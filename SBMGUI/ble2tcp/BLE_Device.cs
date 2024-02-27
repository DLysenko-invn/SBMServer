using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace BLE2TCP
{








    class BLEConnection : BaseConnection, BLECallbackProcessor, IConnection
    {
        const int COMMUNICATION_TIMEOUT_MS = 3000;


        BLEDevice _dev=null;


        

        public BLEConnection(ILog log, string devid, BLECallbackProcessor proc)
        {
            if (!Init(log, devid, proc))
                return;

            _dev = new BLEDevice(this, log,  _uuid);

        }

        public bool Connect()
        {
            Task<bool> t = _dev.Connect();
            t.Wait();
            return t.Result;

        }





        public void BLECallback(string device_uuid, BLECallbackAction operation, string service_uuid, string characteristic_uuid, byte[] data)
        {
            //Debug.WriteLine("BLECallback {0}", operation);
            if ((operation == BLECallbackAction.Connected) || (operation == BLECallbackAction.Disconnected))
            {
                _isconnected = (operation == BLECallbackAction.Connected);
            }
            _proc.BLECallback(_uuid, operation, service_uuid, characteristic_uuid, data);
        }





        BLECharacteristic Find(string service_uuid, string characteristic_uuid)
        {
            BLECharacteristic c =  _dev?.Find(service_uuid, characteristic_uuid);
            if (c == null)
                _log.LogWarning("Not found " + service_uuid + "/" + characteristic_uuid);
            return c;

        }

      



        public async Task<bool> Subscribe(string service_uuid, string characteristic_uuid)
        {
            BLECharacteristic c = _dev?.Find(service_uuid, characteristic_uuid);
            if (c == null)
                return false;
            try
            {   DebugLog("Subscribe",service_uuid, characteristic_uuid);
                await c.Subscribe();
                return true;
            }
            catch(Exception e)
            {   DebugLogError("Subscribe",e.Message,service_uuid, characteristic_uuid);
                return false;
            }
            
        }

        public async Task<bool> Unsubscribe(string service_uuid, string characteristic_uuid)
        {
            BLECharacteristic c =  _dev?.Find(service_uuid, characteristic_uuid);
            if (c == null)
                return false;
            try
            {   DebugLog("Unsubscribe",service_uuid, characteristic_uuid);
                await c.Unsubscribe();
                return true;
            }
            catch(Exception e)
            {   DebugLogError("Unsubscribe",e.Message,service_uuid, characteristic_uuid);
                return false;
            }

        }

        public  void CheckSubscribe(string service_uuid, string characteristic_uuid)
        {
            BLECharacteristic c =  _dev?.Find(service_uuid, characteristic_uuid);
            if (c == null)
                return;
            c.CheckSubscribe();
        }

        public async Task<byte[]> Read(string service_uuid, string characteristic_uuid)
        {
            BLECharacteristic c =  _dev?.Find(service_uuid, characteristic_uuid);
            if (c == null)
                return null;
            try
            {   DebugLog("Read request",service_uuid, characteristic_uuid);
                return await c.Read();
            }
            catch(Exception e)
            {   DebugLogError("Read request",e.Message,service_uuid, characteristic_uuid);
                return null;
            }
            
        }


        public bool IsExists(string service_uuid, string characteristic_uuid)
        { 
            return  (_dev?.Find(service_uuid, characteristic_uuid)) != null;
        }




        public async Task<bool> Write(string service_uuid, string characteristic_uuid,byte[] data)
        {
            BLECharacteristic c =  _dev?.Find(service_uuid, characteristic_uuid);
            if (c == null)
                return false;
            try
            {   DebugLog("Write",service_uuid, characteristic_uuid, data);
                await c.Write(data);
                return true;
            }
            catch(Exception e)
            {   
                DebugLogError("Write",e.Message,service_uuid, characteristic_uuid, data);
                return false;
            }
        }


        public async Task<bool> Disconnect()
        { 
            try
            {   DebugLog("Disconnect attempt");
                await _dev.Disconnect().ConfigureAwait(false);
                return true;
            }
            catch(Exception e)
            {   DebugLogError("Disconnect attempt",e.Message);
                return false;
            }
            
        }


        public async Task<bool> Pair()
        { 


            try
            {   DebugLog("Pair attempt");
                DevicePairingResultStatus result = await _dev.Pair().ConfigureAwait(false);
                DebugLog("Pair result "+result.ToString());
                _log.LogLine("Pair result "+result.ToString());
                return true;
            }
            catch(Exception e)
            {   _log.LogError("BLE Pair exception '"+e.Message+"'");
                DebugLogError("Pair attempt",e.Message);
                return false;
            }
        }


        public async Task<bool> Unpair()
        { 
            try
            {   DebugLog("Unpair attempt");
                DeviceUnpairingResultStatus result = await _dev.Unpair().ConfigureAwait(false);
                DebugLog("Unpair result "+result.ToString());
                _log.LogLine("Unpair result "+result.ToString());
                return true;
            }
            catch(Exception e)
            {   _log.LogError("BLE Unpair exception '"+e.Message+"'");
                DebugLogError("Unpair attempt",e.Message);
                return false;
            }
        }



        public void Dispose()
        {
            Task<bool> t =  Disconnect();
            t.Wait(COMMUNICATION_TIMEOUT_MS);
            
        }



    }


    class BLEBase
    {

        protected BLECallbackProcessor _proc;
        protected ILog _log;
        protected string _uuid;

        public BLEBase(BLECallbackProcessor proc, ILog log, string uuid)
        {
            _log = log;
            _uuid = uuid;
            _proc = proc;
        }

        public string UUID
        {
            get { return _uuid; }
        }




    }



    class BLEDevice: BLEBase
    {

        readonly int E_DEVICE_NOT_AVAILABLE = unchecked((int)0x800710df); // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE)


        BluetoothLEDevice _bledev =null;
        Dictionary<string,BLEService> _services;


        public BLEDevice(BLECallbackProcessor sender, ILog log, string uuid) :base(sender,log,uuid)
        {
            Debug.Assert(_uuid.Length != 0);
            _services = new Dictionary<string, BLEService>();

        }


        public BLECharacteristic Find(string service_uuid, string characteristic_uuid)
        {

            BLECharacteristic c;
            if (_services.ContainsKey(service_uuid))
            {   c = _services[service_uuid].Find(characteristic_uuid);
            } else
            {   c = null;
                _log.LogWarning("Service not found "+ service_uuid+"/"+ characteristic_uuid);
            }
            return c;
        }


        public async Task Disconnect()
        {
            if (_bledev == null)
                return;
         
            _bledev.ConnectionStatusChanged -= ConnectionStatusChanged;

            foreach (BLEService s in _services.Values)
                await s.Disconnect();

            _services.Clear();

            _bledev.Dispose();
            _bledev = null;

        }


        public async Task<DevicePairingResultStatus> Pair()
        { 
            if (_bledev.DeviceInformation.Pairing.IsPaired)
                return DevicePairingResultStatus.AlreadyPaired;
            _log.LogLine("Pair attempt");
            _bledev.DeviceInformation.Pairing.Custom.PairingRequested += CustomOnPairingRequested;
            DevicePairingResult result = await _bledev.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly, DevicePairingProtectionLevel.Default);
            _bledev.DeviceInformation.Pairing.Custom.PairingRequested -= CustomOnPairingRequested;
            return result.Status;
            
        }

        private void CustomOnPairingRequested( DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            _log.LogLine("Pair request accepted.");
            args.Accept();

        }













        public async Task<DeviceUnpairingResultStatus> Unpair()
        { 

            if (!_bledev.DeviceInformation.Pairing.IsPaired)
                return DeviceUnpairingResultStatus.AlreadyUnpaired;

           _log.LogLine("Unpair attempt.");
           DeviceUnpairingResult r= await _bledev.DeviceInformation.Pairing.UnpairAsync();
           return r.Status;
            
       }


        public string MACAddressStr
        {
            get
            {
                if (_bledev == null)
                    return string.Empty;
                byte[] m = BitConverter.GetBytes(_bledev.BluetoothAddress);
                return string.Format("{0:X02}-{1:X02}-{2:X02}-{3:X02}-{4:X02}-{5:X02}-{6:X02}-{7:X02}", m[7], m[6], m[5], m[4], m[3], m[2], m[1], m[0]);
            }


        }




        
        public async Task<bool> Connect()
        {
            Debug.Assert(_bledev == null);
            Debug.Assert(_services.Count == 0);
            Debug.Assert(_uuid != null);


            try
            {
                _log.LogLine("Connecting " + _uuid + "...");
                _bledev = await BluetoothLEDevice.FromIdAsync(_uuid);

                if (_bledev == null)
                {
                    _log.LogError("Failed to connect to device.");
                    _bledev = null;
                    return false;
                }
                _bledev.ConnectionStatusChanged += ConnectionStatusChanged;
            }
            catch (Exception ex) when (ex.HResult == E_DEVICE_NOT_AVAILABLE)
            {
                _log.LogError("Bluetooth radio is not on.");
                _bledev = null;
                return false;
            }
            catch (Exception ex)
            { 
                _log.LogError("Create BLE device error: "+ ex.Message);
                _bledev = null;
                return false; 
            }

            ConnectionStatusChangedEx(_bledev.ConnectionStatus);

            _log.LogLine("Device " + _uuid);


            _log.LogLine("Pairing disabled");
            //DevicePairingResultStatus r = await Pair();
            //_log.LogLine("Pair result  " + r.ToString());


            bool result;

            try
            {
                result = await MakeServicesList().ConfigureAwait(false);
            }
            catch (Exception ex)
            {   _log.LogError("MakeServicesList error: "+ex.Message);
                _bledev = null;
                return false;
            }


            return result;
        }




        private void ConnectionStatusChangedEx(BluetoothConnectionStatus s)
        {   bool isconnected = (s == BluetoothConnectionStatus.Connected);
            _proc.BLECallback(null,isconnected ? BLECallbackAction.Connected : BLECallbackAction.Disconnected, null,null,null);
            _log.LogLine("BLE device " + (isconnected ? "Connected" : "Disconnected") );
        }

        private void ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {   ConnectionStatusChangedEx(sender.ConnectionStatus);
        }

        async Task<bool> MakeServicesList()
        {
            Debug.Assert(_services.Count == 0);

            bool islazyconnect = true;
            //bool islazyconnect = false;

            if (_bledev == null)
            {
                _log.LogError("Device is not connected");
                return false;
            }

            GattDeviceServicesResult result = await _bledev.GetGattServicesAsync(BluetoothCacheMode.Uncached);

            _log.LogLine("Lazy connect mode "+ (islazyconnect ? "enabled" : "disabled"));

            if (result.Status == GattCommunicationStatus.Success)
            {
                IReadOnlyList<GattDeviceService> services = result.Services;
                _log.LogLine(services.Count.ToString()+ " services found");

                int counter=0;
                foreach (GattDeviceService service in services)
                {   string uuid = service.Uuid.ToString();
                    BLEService s = new BLEService(_proc, _log,  uuid, service);
                    if (!islazyconnect)
                    {   s.Connect();
                    } else
                    {  // BLEConnection.DebugLogEx(_log, (counter + 1).ToString() + ".", uuid, null, null);
                    }
                    //todo: add progress
                    _services[uuid] = s;
                    
                    counter++;
                    
                }


            } else
            {   _log.LogError("Device unreachable");
                return false;
            }

            return true;
        }
    }







    class BLEService : BLEBase
    {
        GattDeviceService _bleserv;
        Dictionary<string,BLECharacteristic> _chars;


        
        public BLEService(BLECallbackProcessor sender, ILog log, string uuid, GattDeviceService service) : base(sender, log, uuid)
        {
            _bleserv = service;
            _chars = null;

        }


        public BLECharacteristic Find(string characteristic_uuid)
        {   if (_chars==null)
            {   
                Connect();
                //t.Wait();

                //await Connect(); 
            }

            if (_chars.ContainsKey(characteristic_uuid))
            {   return  _chars[characteristic_uuid];
            } 
            
            _log.LogWarning("Characteristic not found "+ _bleserv.Uuid +"/"+ characteristic_uuid);
            return null;
        }


        public void Connect()
        {
            Debug.Assert(_chars==null);
            _chars = new Dictionary<string, BLECharacteristic>();
            MakeCharacteristicsList().Wait();

        }

        async Task<bool> MakeCharacteristicsList()
        {
            Debug.Assert(_chars.Count == 0);
            Debug.Assert(_bleserv != null);

            IReadOnlyList<GattCharacteristic> characteristics = null;
            try
            {
                DeviceAccessStatus accessStatus = await _bleserv.RequestAccessAsync().AsTask().ConfigureAwait(false);
                if (accessStatus == DeviceAccessStatus.Allowed)
                {
                    GattCharacteristicsResult result = await _bleserv.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (result.Status == GattCommunicationStatus.Success)
                    {   characteristics = result.Characteristics;
                        //_log.LogLine(characteristics.Count.ToString() + " characteristics found");
                    } else
                    {   _log.LogError("Error accessing service " + _uuid);
                    }
                } else
                {    _log.LogError("Error accessing service "+_uuid);
                }
            }
            catch (Exception ex)
            {
                _log.LogError("Restricted service. Can't read characteristics: " + ex.Message);
                return false;
            }

            if (characteristics == null)
            {
                _log.LogError("Service error. No characteristics.");
                return false;
            }

            foreach (GattCharacteristic chr in characteristics)
            {
                string uuid = chr.Uuid.ToString();
                BLECharacteristic c = new BLECharacteristic(_proc,_log, uuid, this, chr);
                await c.Connect().ConfigureAwait(false);//todo: make it lazy
                _chars[uuid] = c;
            }

            return true;

        }

        public async Task Disconnect()
        {
            if (_chars!=null)
            {
                foreach (BLECharacteristic c in _chars.Values)
                {   await c.Disconnect();
                }
                _chars.Clear();
                _chars = null;
            }

            _bleserv.Dispose();
            _bleserv = null;

        }

    }


    class BLECharacteristic : BLEBase
    {
        GattCharacteristic _blechr;
        GattCharacteristicProperties? _bleprop = null;
        BLEService _serv;
        bool _issubscribed;




        public BLECharacteristic(BLECallbackProcessor sender, ILog log, string uuid, BLEService service, GattCharacteristic characteristic) : base(sender, log, uuid)
        {
            _serv = service;
            _blechr = characteristic;
            _issubscribed = false;

        }

        public async Task<bool> Connect()
        {
            GattDescriptorsResult result = await _blechr.GetDescriptorsAsync(BluetoothCacheMode.Uncached);
            if (result.Status != GattCommunicationStatus.Success)
            { _log.LogError("Descriptor read failure: " + result.Status.ToString());
                return false;
            }

            _bleprop = _blechr.CharacteristicProperties;

            string s = "";
            if (HasFlag(GattCharacteristicProperties.Read))
                s += " R";
            if (HasFlag(GattCharacteristicProperties.Write))
                s += " W";
            if (HasFlag(GattCharacteristicProperties.WriteWithoutResponse))
                s += " w";
            if (HasFlag(GattCharacteristicProperties.Indicate))
                s += " I";
            if (HasFlag(GattCharacteristicProperties.Notify))
                s += " N";

            BLEConnection.DebugLogEx(_log,"Connect", _serv.UUID, _uuid, s.Trim() );

            return true;
        }



        public async Task Disconnect()
        {

            if (_blechr == null)
                return;

            await Unsubscribe();

            _blechr = null;
            _bleprop = null;
        }


        bool HasFlag(GattCharacteristicProperties f)
        {
            return _bleprop == null ? false : ((GattCharacteristicProperties)_bleprop).HasFlag(f);
        }

        public async Task<byte[]> Read()
        {
            if (_bleprop == null)
                return null;

            if (!HasFlag(GattCharacteristicProperties.Read))
                return null;


            GattReadResult resultv = await _blechr.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (resultv.Status != GattCommunicationStatus.Success)
            {   _log.LogError("Read failed " + resultv.Status.ToString());
                return null;
            } else
            {
                //_log.LogLine("Read " + _serv.UUID + "/" + UUID +"   "+ resultv.Value.Length.ToString()+" bytes");
            }

            byte[] data = IBuffer2Bytes(resultv.Value);

            return data;

        }

        public async Task<bool> Write(byte[] data)
        {
            if (_bleprop == null)
                return false;

            IBuffer d = data.AsBuffer();
            GattWriteResult wr = null;

            if (HasFlag(GattCharacteristicProperties.Write))
            {   wr = await _blechr.WriteValueWithResultAsync(d, GattWriteOption.WriteWithResponse);
            } else
            if (HasFlag(GattCharacteristicProperties.WriteWithoutResponse))
            {   wr = await _blechr.WriteValueWithResultAsync(d, GattWriteOption.WriteWithoutResponse);
            } else
            {   _log.LogError("Write not supported for "+_blechr.Uuid.ToString());
                return false;    
            }

            if (wr.Status != GattCommunicationStatus.Success)
            {   _log.LogError("WriteWithResponse " + wr.Status.ToString()+  (wr.ProtocolError == null ? "" : string.Format(" protocol error 0x{0:X2}", wr.ProtocolError)));
                return false;
            }

            //_log.LogLine("WriteWithResponse " + wr.Status.ToString() + (wr.ProtocolError == null ? "" : string.Format(" protocol error 0x{0:X2}", wr.ProtocolError)));

            return true;

        }


        static byte[] EMPTYBYTES = new byte[] { };
        static byte[] IBuffer2Bytes(IBuffer buf)
        {
            if (buf == null)
                return EMPTYBYTES;
            CryptographicBuffer.CopyToByteArray(buf, out byte[] data);
            return data;
        }


        void Send(BLECallbackAction operation, IBuffer buf=null)
        {
            _proc.BLECallback(null,operation,_serv.UUID, UUID, IBuffer2Bytes(buf) );
        }


        private void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            Send(BLECallbackAction.Notification, args.CharacteristicValue);
        }



        private void AddValueChangedHandler()
        {
            if (!_issubscribed)
            {   _blechr.ValueChanged += Characteristic_ValueChanged;
                _issubscribed = true;
            }
        }


        private void RemoveValueChangedHandler()
        {
            if (_issubscribed)
            {   _blechr.ValueChanged -= Characteristic_ValueChanged;
                _issubscribed = false;
            }
        }



        public bool CheckSubscribe()
        {
            
            return _issubscribed;
        }


           
        public async Task<bool> Subscribe()
        {
            if (_bleprop == null)
                return false;

            if (_issubscribed)
                return true;

            GattCharacteristicProperties prop = ((GattCharacteristicProperties)_bleprop);

            var cccd_value = GattClientCharacteristicConfigurationDescriptorValue.None;
            if (prop.HasFlag(GattCharacteristicProperties.Indicate))
            {   cccd_value = GattClientCharacteristicConfigurationDescriptorValue.Indicate;
            } else 
            if (prop.HasFlag(GattCharacteristicProperties.Notify))
            {   cccd_value = GattClientCharacteristicConfigurationDescriptorValue.Notify;
            }

            if (cccd_value == GattClientCharacteristicConfigurationDescriptorValue.None)
            {
                _log.LogError("Can not subscribe");
                return false;
            }

            bool rc = false;
            try
            {   GattCommunicationStatus result = await _blechr.WriteClientCharacteristicConfigurationDescriptorAsync(cccd_value);
                if (result == GattCommunicationStatus.Success)
                {
                    _blechr.ValueChanged += Characteristic_ValueChanged;
                    _issubscribed = true;
                    rc = true;

                } else
                {   _log.LogError("Error registering for value changes: " + result.ToString());
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _log.LogError("Exception registering for value changes:" + ex.Message);
            }


            return rc;
        }


        public async Task<bool> Unsubscribe()
        {
            if (_bleprop == null)
                return false;

            if (!_issubscribed)
                return true;

            if (_blechr==null)
                return false;

            bool rc = false;
            try
            {
                GattCommunicationStatus result = await _blechr.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                if (result == GattCommunicationStatus.Success)
                {
                    _blechr.ValueChanged -= Characteristic_ValueChanged;
                    _issubscribed = false;
                    rc = true;
                }
                else
                {   _log.LogError("Error un-registering for value changes: " + result.ToString());
                }
            }
            catch (UnauthorizedAccessException ex)
            {   _log.LogError("Exception un-registering for value changes:" + ex.Message);
            }


            return rc;
        }




    }







}
