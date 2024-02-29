using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;

namespace BLE2TCP
{



    static class PayloadFactory
    {
        static IPayload NOPAYLOAD = new PacketPayloadNothing();

        static Dictionary<PacketOpCode, Type> _tab = new Dictionary<PacketOpCode, Type>()
        {
            [PacketOpCode.write            ] = typeof(PacketPayloadBytes                       ),
            [PacketOpCode.read             ] = typeof(PacketPayloadNothing                     ),
            [PacketOpCode.connect          ] = typeof(PacketPayloadJSON<PayloadDeviceInfo>     ),
            [PacketOpCode.disconnect       ] = typeof(PacketPayloadNothing                     ),
            [PacketOpCode.set_param        ] = typeof(PacketPayloadJSON<PayloadServerParam>    ),
            [PacketOpCode.get_param        ] = typeof(PacketPayloadNothing                     ),
            [PacketOpCode.run_watcher      ] = typeof(PacketPayloadNothing                     ),
            [PacketOpCode.stop_watcher     ] = typeof(PacketPayloadNothing                     ),
            [PacketOpCode.subscribe        ] = typeof(PacketPayloadNothing                     ),
            [PacketOpCode.unsubscribe      ] = typeof(PacketPayloadNothing                     ),
            [PacketOpCode.get_sc           ] = typeof(PacketPayloadJSON<PayloadSC>             ),

        };



        public static IPayload MakePayload(PacketHeader header, byte[] data, int startindex, int size)
        {
            if (size == 0)
                return NOPAYLOAD;

            
            if (header.Group != PacketOpType.command)
            {   Debug.Assert(false);
                return NOPAYLOAD;
            }

            if (header.IsError)
            {   Debug.Assert(false);
                return NOPAYLOAD;
            }

            if (!_tab.ContainsKey(header.Code))
            {   Debug.Assert(false);
                return NOPAYLOAD;
            }

            if (_tab[header.Code] == typeof(PacketPayloadNothing))
                return NOPAYLOAD;

            IPayload p= Activator.CreateInstance(_tab[header.Code]) as IPayload;

            if (p == null)
            {   Debug.Assert(false);
                return NOPAYLOAD;
            }

            p.FromBytes(data, startindex, size);
            return p;


        }
    }


    class Core : IPacketParser,  BLECallbackProcessor
    {
        delegate PacketBase ProcessPacketDelegate(PacketBase packet); 
        IPacketSender _transport;
        ILog _log;
        IServerStatus _status;
        IWatcher _watcher;
        BLEIndexer _indexer;
        Dictionary<string, IConnection> _connections = new Dictionary<string, IConnection>();



        Dictionary<PacketOpCode,ProcessPacketDelegate> _proctab;
        Dictionary<BLECallbackAction,PacketOpCode> _msgconvert;

        public Core(ILog log,IServerStatus status,IPacketSender transport)
        { 
            _log =log;
            _status = status;
            _transport = transport;
            _watcher = new DEVWatcher(_log,_transport);
            _indexer = new BLEIndexer();

            _proctab = new Dictionary<PacketOpCode, ProcessPacketDelegate>
            { 
                [PacketOpCode.write            ] = new ProcessPacketDelegate(PP_Write)       ,
                [PacketOpCode.read             ] = new ProcessPacketDelegate(PP_Read)        ,
                [PacketOpCode.connect          ] = new ProcessPacketDelegate(PP_Connect)     ,  
                [PacketOpCode.disconnect       ] = new ProcessPacketDelegate(PP_Disconnect)  ,  
                [PacketOpCode.set_param        ] = new ProcessPacketDelegate(PP_SetParam)    ,
                [PacketOpCode.get_param        ] = new ProcessPacketDelegate(PP_GetParam)    ,
                [PacketOpCode.run_watcher      ] = new ProcessPacketDelegate(PP_RunRadar)    , 
                [PacketOpCode.stop_watcher     ] = new ProcessPacketDelegate(PP_StopRadar)   ,  
                [PacketOpCode.subscribe        ] = new ProcessPacketDelegate(PP_Subscribe)   ,
                [PacketOpCode.unsubscribe      ] = new ProcessPacketDelegate(PP_Unsubscribe) ,
                [PacketOpCode.get_sc           ] = new ProcessPacketDelegate(PP_GetSC)       ,


            };

            _msgconvert = new Dictionary<BLECallbackAction, PacketOpCode>
            {
                [BLECallbackAction.Notification   ] = PacketOpCode.notification ,
                [BLECallbackAction.Connected      ] = PacketOpCode.connected ,
                [BLECallbackAction.Disconnected   ] = PacketOpCode.disconnect ,


            };
        }


        public PacketBase ProcessPacket(PacketBase packet)
        {
            
            if (packet.Header.Group!= PacketOpType.command)
            {
                _log.LogWarning("Unsupported packet");
                return null;
            }

            return _proctab[packet.Header.Code](packet);

        }


        public void ProcessPacketReset()
        {
            //todo: close all connections

            _watcher.Stop();
            DisconnectAll();

        }


        public void BLECallback(string devid, BLECallbackAction operation, string service_uuid, string characteristic_uuid, byte[] data)
        {

            //Debug.WriteLine("&&& {0} {1}",operation.ToString(),devid);

            int devindex = _indexer.GetDevIndex(devid);
            int scindex = _indexer.GetSCIndex(devid, service_uuid, characteristic_uuid);

            PacketOpCode c;
            if (!_msgconvert.ContainsKey(operation))
            {   Debug.Assert(false);
                c = PacketOpCode.nop;
            } else
            {   c = _msgconvert[operation];
            }

            _transport.SendPacket(PM.BLEDataIndication(c, devindex, scindex, data));

        }






        #region BLE Watcher

        PacketBase  PP_RunRadar(PacketBase packet)
        {
            if (!_watcher.IsStopped)
                return PM.ResponseOk(packet, "Already started",ResponseCode.AlreadyStarted);

            try
            {
                _watcher.Start();
                return PM.ResponseOk(packet, "BLE Watcher started");
            }
            catch(Exception e)
            {
                return PM.ResponseError(packet, ResponseCode.ErrorBLE, "BLE Watcher start error: " + e.Message);
            }

            


        }

        PacketBase PP_StopRadar(PacketBase packet)
        { 
            _watcher.Stop();

            return PM.ResponseOk(packet, "BLE Watcher stopped");

        }

        #endregion

        #region DEVICE

        PacketBase PP_Connect(PacketBase packet)
        {
            PayloadDeviceInfo i = (packet.Payload as PacketPayloadJSON<PayloadDeviceInfo>)?.Data;
            Debug.Assert(i != null);
            if (i==null)
                return PM.ResponseError(packet, ResponseCode.ErrorFormat, "Device id expected in the packet payload");

            int devindex;
            if (_connections.ContainsKey(i.Id))
            {
                if (_connections[i.Id].IsConnected)
                {
                    devindex = _indexer.GetDevIndex(i.Id);
                    return PM.ResponseOkDev(packet, devindex, "Connection started", i.Id, ResponseCode.AlreadyStarted);
                }

                IConnection c = _connections[i.Id];
                _connections.Remove(i.Id);
                c.Dispose();
            }

            _connections[i.Id] = ConnectionFactory.Create(_log, i.Id , this);
            
            devindex = _indexer.GetDevIndex(i.Id);

            _transport.SendPacket(PM.ResponseOkDev(packet, devindex, "Connection started", i.Id));

            _connections[i.Id].Connect();

            return null;

        }





        void DisconnectAll()
        {

            string[] ids = _connections.Keys.ToArray();

            foreach (string devid in ids)
            {
                int devindex = _indexer.GetDevIndex(devid);
                IConnection c = _connections[devid];
                _connections.Remove(devid);
                c.Dispose();
                
                _indexer.RemoveDevId(devindex);
            }

        }


        PacketBase PP_Disconnect(PacketBase packet)
        {

            string devid = _indexer.GetDevId(packet.Header.DevIndex);
            if (!_connections.ContainsKey(devid))
                return PM.ResponseError(packet, ResponseCode.ErrorNotFound, "Not connected yet");

            IConnection c = _connections[devid];
            _connections.Remove(devid);
            c.Dispose();

            _indexer.RemoveDevId(packet.Header.DevIndex);

            return PM.ResponseOk(packet, "Device disconnected");
        }


        PacketBase GetFromIndex(PacketBase packet,out string devid, out string service_uuid, out string characteristic_uuid)
        {
            _indexer.Get(packet.Header.DevIndex, packet.Header.SCIndex, out  devid, out  service_uuid, out  characteristic_uuid);

            if (string.IsNullOrEmpty(devid))
                return PM.ResponseError(packet, ResponseCode.ErrorNotFound, "Wrong device index");
            if ((string.IsNullOrEmpty(service_uuid)) || (string.IsNullOrEmpty(characteristic_uuid)))
                return PM.ResponseError(packet, ResponseCode.ErrorNotFound, "Wrong  sc index");

            if (!_connections.ContainsKey(devid))
                return PM.ResponseError(packet, ResponseCode.ErrorNotFound, "Not connected yet");

            return null;
        }


        PacketBase PP_Subscribe(PacketBase packet)
        {
            PacketBase error_packet = GetFromIndex(packet, out string devid, out string service_uuid, out string characteristic_uuid);
            if (error_packet != null)
                return error_packet;

            IConnection c = _connections[devid];

            Task<bool> t = c.Subscribe(service_uuid, characteristic_uuid);
            t.Wait();
            if (!t.Result)
                return PM.ResponseError(packet, ResponseCode.ErrorBLE, "Subscribe error");

            return PM.ResponseOkSC(packet, "Subscribed", service_uuid, characteristic_uuid);
        }





        PacketBase PP_Unsubscribe(PacketBase packet)
        {
            PacketBase error_packet = GetFromIndex(packet, out string devid, out string service_uuid, out string characteristic_uuid);
            if (error_packet != null)
                return error_packet;

            IConnection c = _connections[devid];

            Task<bool> t = c.Unsubscribe(service_uuid, characteristic_uuid);
            t.Wait();
            if (!t.Result)
                return PM.ResponseErrorSC(packet, ResponseCode.ErrorBLE, "Unsubscribe error", service_uuid, characteristic_uuid);


            return PM.ResponseOkSC(packet, "Unsubscribed", service_uuid, characteristic_uuid);

        }

        PacketBase PP_Read(PacketBase packet)
        {
            PacketBase error_packet = GetFromIndex(packet, out string devid, out string service_uuid, out string characteristic_uuid);
            if (error_packet != null)
                return error_packet;

            IConnection c = _connections[devid];

            Task<byte[]> t = c.Read(service_uuid, characteristic_uuid);
            t.Wait();
            if (t.Result==null)
                return PM.ResponseErrorSC(packet, ResponseCode.ErrorBLE, "Read error", service_uuid, characteristic_uuid);
            
            return PM.BLEDataResponse(packet, t.Result);

        }

        PacketBase PP_Write(PacketBase packet)
        {
            PacketBase error_packet = GetFromIndex(packet, out string devid, out string service_uuid, out string characteristic_uuid);
            if (error_packet != null)
                return error_packet;

            IConnection c = _connections[devid];

            byte[] data = packet.Payload?.ToBytes();
            Debug.Assert(data != null);
            if (data == null)
                return PM.ResponseErrorSC(packet, ResponseCode.ErrorFormat, "Nothing to write", service_uuid, characteristic_uuid);


            Task<bool> t = c.Write(service_uuid, characteristic_uuid,data);
            t.Wait();
            if (!t.Result)
                return PM.ResponseErrorSC(packet, ResponseCode.ErrorBLE, "Write error", service_uuid, characteristic_uuid);
         

            return PM.ResponseOk(packet);
        }

        PacketBase PP_GetSC(PacketBase packet)
        {
            PayloadSC i = (packet.Payload as PacketPayloadJSON<PayloadSC>)?.Data;
            Debug.Assert(i != null);
            if (i == null)
                return PM.ResponseError(packet, ResponseCode.ErrorFormat, "Sevice and characteristic ids expected in the packet payload");

            string devid = _indexer.GetDevId(packet.Header.DevIndex);
            if (string.IsNullOrEmpty(devid))
                return PM.ResponseError(packet, ResponseCode.ErrorNotFound, "Wrong device index");

            if (!_connections.ContainsKey(devid))
                return PM.ResponseError(packet, ResponseCode.ErrorNotFound, "Not connected yet");

            int scindex = _indexer.GetSCIndex(devid, i.ServiceId, i.CharacteristicId);

            return PM.ResponseOkSC(packet, scindex, i.ServiceId, i.CharacteristicId);
            
        }


        #endregion


        PacketBase PP_SetParam(PacketBase packet)
        {
            return PM.ResponseError(packet);
        }

        PacketBase PP_GetParam(PacketBase packet)
        {
            return PM.ResponseError(packet);
        }



    }








}
