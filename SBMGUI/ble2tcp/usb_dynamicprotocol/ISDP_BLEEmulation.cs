using DynamicProtocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BLE2TCP.BLEEMU
{


    interface IDPTalkMaker
    { 
        DPTalk MakeATalk(ISDPFrame frametosend);
    }

    interface IDPPacketProcessor
    {
        bool ProcessCommand(ISDPCmd cmd);

    }




    class DPTalk:Talk
    {
        public static DPTalk NOTALK = new DPTalk() { Stage = TalkStage.DoneOk };

        public ISDPCmd   sendcmd;
        public ISDPCmd   recvcmd;
        public ISDPFrame recvframe;
        public ISDPFrame sendframe;

        public DPTalk():base()
        {
        }

        public DPTalk(ISDPFrame frame):this()
        {
            Debug.Assert(frame != null);
            sendframe = frame;
            sendcmd = new ISDPCmd(frame);
            recvframe = new ISDPFrame();
            recvcmd = null;
            Stage = TalkStage.Ready;
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


    class USBSmartMotionAsBLE :IUSBtoBLEProtocol, IDPTalkMaker
    {


        #if DEBUG
        public const int DEFAULT_TIMEOUT_MS = 5000;
        #else
        public const int DEFAULT_TIMEOUT_MS = 5000;
        #endif


        ILog _log;
        BLECallbackProcessor _proc;
        Dictionary<string,IFakeService> _serv = new Dictionary<string, IFakeService>();
        protected string _uuid;
        IUSBTransport _transport;
        ISDPFrame _currframe;
        List<byte> _buf;
        DPTalk _currtalk;
        Queue<DPTalk> _talkstomake = new Queue<DPTalk>();


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

            FakeServiceBATT          s_bat    = new FakeServiceBATT(proc);
            ISDP_FakeServiceIMU      s_imu    = new ISDP_FakeServiceIMU(proc,this); 
/*
            FakeServiceSIF      s_sif    = new ISDP_FakeServiceSIF(proc,this);
            FakeServiceSIFRAW   s_sifraw = new ISDP_FakeServiceSIFRAW(proc, this);
 
*/


            FakeServiceBase[] servs = new FakeServiceBase[] { s_bat, s_imu }; //, s_sif, s_sifraw  };

            foreach (FakeServiceBase s in servs)
                _serv[s.UUID] = s;


        }











        public uint MinimalRead 
        {   get
            {   return (uint)_currframe.MaxRead;
            }
        }

        public bool IsTimeoutDetected()
        {
            throw new NotImplementedException();
        }

        public void ProcessByte(byte b)
        {

            //Debug.WriteLine("ProcessByte: "+b.ToString());
            _buf.Add(b);
            if (_buf.Count == _currframe.MaxRead)
            {
                int n = _currframe.Put( _buf.ToArray() );
                Debug.Assert(n==_buf.Count);
                _buf.Clear();

                if (_currframe.IsError)
                {   //todo: error
                    Debug.WriteLine("ProcessByte: _currframe.IsError");
                }

                if (_currframe.IsFinished)
                {
                    ISDPFrame f = _currframe;
                    _currframe = new ISDPFrame();
                    ProcessFrame(f);
                }

            }


        }


        public void Reset()
        {
            _currframe = new ISDPFrame();
            _buf.Clear();

            lock (this)
            {   _currtalk = DPTalk.NOTALK;
                _talkstomake.Clear();
            }
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







        void MakeATalkUnsafe(DPTalk t)
        { 
            if (t==null)
                return;

            Debug.WriteLine("@@@ MakeATalk " + t.sendcmd.ToString());
            Debug.Assert(_currtalk.IsDone);

            _currtalk = t;
            //CommLog("W "+_currtalk.sendcmd.ToString());
            //Log("Write",_currtalk.sendcmd.ToString());
            _transport.WriteBytes(_currtalk.sendframe.ToBytes());
            _currtalk.Stage = TalkStage.Waiting;

        }

        public DPTalk MakeATalk(ISDPFrame frametosend)
        {
            DPTalk t;
            t = new DPTalk(frametosend);
            t.Stage = TalkStage.InQueue;
            t.timeout_ms = DEFAULT_TIMEOUT_MS;
            t.starttime = DateTime.Now;

            lock (this)
            {
                if (_currtalk.Stage == TalkStage.Waiting)
                {   _talkstomake.Enqueue(t);
                    Debug.WriteLine("@@@ MakeATalk Enqueue " + t.sendcmd.ToString());
                } else
                {   MakeATalkUnsafe(t);
                }

            }

            return t;

        }





        void ProcessFrame(ISDPFrame f)
        {

            ISDPCmd cmd = new ISDPCmd(f);

            if (cmd.ETYPE == ISDP.ETYPE_RESP)
            { 



                lock (this)
                {
                    _currtalk.recvframe = f;
                    _currtalk.recvcmd = cmd;
                    _currtalk.Stage = TalkStage.DoneOk;

                }

                return;
            }

            if (cmd.ETYPE == ISDP.ETYPE_ASYNC)
            {

                foreach(IFakeService s in _serv.Values)
                {
                    IDPPacketProcessor pp = s.NotifyCharacteristic as IDPPacketProcessor;
                    if (pp==null)
                        continue;
                    bool rc = pp.ProcessCommand(cmd);
                    if (rc)
                        return;
                }

                Debug.Assert(false,"unsupported command async type");

                return;
            }





            if (cmd.ETYPE == ISDP.ETYPE_CMD)
            {
                Debug.Assert(false,"unsupported command cmd type");
                //CommLog("? " + p.LogString() );
                return;
            }




            Debug.Assert(false,"unsupported command");
            //CommLog("? " + p.LogString() );
            return;
            
        }

    }




}
