using DynamicProtocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BLE2TCP.BLEEMU
{


    class DPTalk:Talk<ISDPFrame,ISDPCmd>
    {
        public static DPTalk NOTALK = new DPTalk() { Stage = TalkStage.DoneOk };

        public DPTalk():base()
        {
        }

        public DPTalk(ISDPFrame send, ISDPCmd info):base(send, info)
        {
        }
    }


    class DPUSBConnection : USBConnection
    {

        protected override uint BAUD => 2000000;

        //const string VIDPIDMARK = "VID_1915&PID_520F";

        public static bool CheckDeviceId(string devid)
        {
            return true;
            //return devid.ToUpper().Contains(VIDPIDMARK);
        }

        public DPUSBConnection(ILog log, string devid, BLECallbackProcessor proc):base(log, devid, proc)
        {
            if (_initresult)
            { 
                _prot = new USBSmartMotionAsBLE(log, devid, this ,this);
            }
        }


    }


    class USBSmartMotionAsBLE :IUSBtoBLEProtocol
    {

        ILog _log;
        BLECallbackProcessor _proc;
        Dictionary<string,IFakeService> _serv = new Dictionary<string, IFakeService>();
        protected string _uuid;
        IUSBTransport _transport;
        ISDPFrame _currframe;
        List<byte> _buf;


        public USBSmartMotionAsBLE(ILog log, string devid, BLECallbackProcessor proc, IUSBTransport transport)
        {

            Debug.Assert(log != null);
            Debug.Assert(proc != null);
            Debug.Assert(transport != null);
            _log = log;
            _proc = proc;
            _uuid = devid;
            _transport = transport;

            _buf = new List<byte>();
            Reset();

            FakeServiceBATT     s_bat    = new FakeServiceBATT(proc);
/*
            FakeServiceSIF      s_sif    = new ISDP_FakeServiceSIF(proc,this);
            FakeServiceSIFRAW   s_sifraw = new ISDP_FakeServiceSIFRAW(proc, this);
            FakeServiceIMU      s_imu    = new ISDP_FakeServiceIMU(proc,this);            
*/


            FakeServiceBase[] servs = new FakeServiceBase[] { s_bat }; //, s_sif, s_sifraw, s_imu };

            foreach (FakeServiceBase s in servs)
                _serv[s.UUID] = s;




        }











        public uint MinimalRead =>  (uint)_currframe.MaxRead;

        public bool IsTimeoutDetected()
        {
            throw new NotImplementedException();
        }

        public void ProcessByte(byte b)
        {

            _buf.Add(b);
            if (_buf.Count == _currframe.MaxRead)
            {
                int n = _currframe.Put( _buf.ToArray() );
                Debug.Assert(n==_buf.Count);
                _buf.Clear();

                if (_currframe.IsError)
                {   //todo: error
                }

                if (_currframe.IsFinished)
                {
                    ProcessFrame(_currframe);
                    _currframe = new ISDPFrame();
                }

            }


        }


        public void Reset()
        {
            _currframe = new ISDPFrame();
            _buf.Clear();
        }


        public byte[] Read(string service_uuid, string characteristic_uuid)
        {   return (_serv.ContainsKey(service_uuid.ToLower())) ? _serv[service_uuid.ToLower()].Read(characteristic_uuid.ToLower()) : null;
        }
        public bool Write(string service_uuid, string characteristic_uuid, byte[] data)
        {   return (_serv.ContainsKey(service_uuid.ToLower())) ? _serv[service_uuid.ToLower()].Write(characteristic_uuid.ToLower(), data) : false;
        }
        public bool Subscribe(string service_uuid, string characteristic_uuid)
        {   return (_serv.ContainsKey(service_uuid.ToLower())) ? _serv[service_uuid.ToLower()].Subscribe(characteristic_uuid.ToLower()) : false;
        }
        public bool Unsubscribe(string service_uuid, string characteristic_uuid)
        {   return (_serv.ContainsKey(service_uuid.ToLower())) ? _serv[service_uuid.ToLower()].Unsubscribe(characteristic_uuid.ToLower()) : false;
        }


        void ProcessFrame(ISDPFrame f)
        {
            
        }


    }




}
