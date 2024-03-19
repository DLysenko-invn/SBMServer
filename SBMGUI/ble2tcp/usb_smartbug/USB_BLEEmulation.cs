using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;


namespace BLE2TCP.BLEEMU
{










    class USBPebbleAsBLE : IUSBtoBLEProtocol , ISBTalkMaker
    {

        #if DEBUG
        public const int DEFAULT_TIMEOUT_MS = 5000;
        #else
        public const int DEFAULT_TIMEOUT_MS = 5000;
        #endif



        ILog _log;
        BLECallbackProcessor _proc;
        protected string _uuid;
        IUSBTransport _transport;
        protected Packet _currpkt;
        SBTalk _currtalk;
        Queue<SBTalk> _talkstomake = new Queue<SBTalk>();
        Dictionary<string,IFakeService> _serv = new Dictionary<string, IFakeService>();
        CmdInfo[] _protcmdinfodata;
        Dictionary<CmdId, CmdInfo> _prot = new Dictionary<CmdId, CmdInfo>();

        public USBPebbleAsBLE(ILog log, string devid, BLECallbackProcessor proc, IUSBTransport transport)
        {
            Debug.Assert(log != null);
            Debug.Assert(proc != null);
            Debug.Assert(transport != null);
            _log = log;
            _proc = proc;
            _uuid = devid;
            _transport = transport;
            _currtalk = SBTalk.NOTALK;
            _currpkt = new Packet();


            FakeServiceBATT     s_bat    = new FakeServiceBATT(proc);
            FakeServiceSIF      s_sif    = new FakeServiceSIF(proc,this);
            FakeServiceSIFRAW   s_sifraw = new FakeServiceSIFRAW(proc, this);
            FakeServiceIMU      s_imu    = new FakeServiceIMU(proc,this);

            _protcmdinfodata = new CmdInfo[] {

                new CmdInfo(CmdId.CMD_SIF_PARAM_WRITE,       CmdType.Write,       true, 5, true, 67,  2, s_sif.RW   ),
                new CmdInfo(CmdId.CMD_SIF_PARAM_READ,        CmdType.Read,        true, 1, true,  3,  2, s_sif.RW   ),
                new CmdInfo(CmdId.EVENT_SIF_DATA,            CmdType.Indication, false, 0, true, 10,  0, s_sif.N    ),

                new CmdInfo(CmdId.CMD_SIF_RAW_PARAM_WRITE,   CmdType.Write,       true, 1, true,  2,  2, s_sifraw.RW ),
                new CmdInfo(CmdId.CMD_SIF_RAW_PARAM_READ,    CmdType.Read,        true, 1, true,  2,  2, s_sifraw.RW ),
                new CmdInfo(CmdId.EVENT_SIF_RAW_DATA,        CmdType.Indication, false, 0, true, 33,  0, s_sifraw.N  ),

                new CmdInfo(CmdId.CMD_IMU_PARAM_WRITE,       CmdType.Write,       true, 1, true,  8,  2, s_imu.RW ),
                new CmdInfo(CmdId.CMD_IMU_PARAM_READ,        CmdType.Read,        true, 1, true,  2,  2, s_imu.RW ),
                new CmdInfo(CmdId.EVENT_IMU_DATA,            CmdType.Indication, false, 0, true, 22,  0, s_imu.N  ),

//(CmdId id, CmdType t, bool issend, int sendsize, bool isrecv, int recvsize_ok, int recvsize_error, IFakeCharacteristic characteristic)

            };

            FakeServiceBase[] servs = new FakeServiceBase[] { s_bat, s_sif, s_sifraw, s_imu };

            foreach (FakeServiceBase s in servs)
                _serv[s.UUID] = s;

            foreach (CmdInfo i in _protcmdinfodata)
                _prot[i.id] = i;

        }






        public uint MinimalRead
        {
            get { return (uint)_currpkt.NextPortion; }
        }


        public bool IsTimeoutDetected()
        {
            lock (this)
            {   if (_currtalk.Stage == TalkStage.Waiting)
                {   TimeSpan dt = DateTime.Now - _currtalk.starttime;
                    if (dt.TotalMilliseconds > _currtalk.timeout_ms)
                        return true;
                }
            }
            return false;
        }

        public void ProcessByte(byte b)
        {
            //Debug.WriteLine("COM R >" + b.ToString());
            bool r = _currpkt.Add(b);

            if (r)
            {
                Packet p = _currpkt;
                _currpkt = new Packet();
                ProcessPacket(p);
            }
        }

        public void Reset()
        {
            _currpkt = new Packet();
            lock (this)
            {   _talkstomake.Clear();
                _currtalk = SBTalk.NOTALK;
            }
        }


        public byte[] Read(string service_uuid, string characteristic_uuid)
        {
            return (_serv.ContainsKey(service_uuid.ToLower())) ? _serv[service_uuid.ToLower()].Read(characteristic_uuid.ToLower()) : null;
        }
        public bool Write(string service_uuid, string characteristic_uuid, byte[] data)
        {
            return (_serv.ContainsKey(service_uuid.ToLower())) ? _serv[service_uuid.ToLower()].Write(characteristic_uuid.ToLower(), data) : false;
        }
        public bool Subscribe(string service_uuid, string characteristic_uuid)
        {
            return (_serv.ContainsKey(service_uuid.ToLower())) ? _serv[service_uuid.ToLower()].Subscribe(characteristic_uuid.ToLower()) : false;
        }
        public bool Unsubscribe(string service_uuid, string characteristic_uuid)
        {
            return (_serv.ContainsKey(service_uuid.ToLower())) ? _serv[service_uuid.ToLower()].Unsubscribe(characteristic_uuid.ToLower()) : false;
        }






        CmdInfo GetInfo(CmdId id)
        {
            return _prot.ContainsKey(id) ? _prot[id] : null;
        }



        void ProcessPacket(Packet p)
        {

            bool isprocesspacket = false;
            CmdInfo pi = GetInfo(p.Cmd);

            if (pi == null)
            {   ErrorLog("Command error " , p.LogString());
                CommLog("E " + p.LogString());
                return;
            }

            lock (this)
            { 

                if (pi.type == CmdType.Indication)
                {
                    CommLog("I " + p.LogString() );
                    //Log("Indication", p.LogString(true));
                    isprocesspacket =true;

                } else
                if (pi.type == CmdType.Write)
                { 
                    if ((_currtalk.Stage == TalkStage.Waiting) && (_currtalk.senddata.Cmd == p.Cmd))
                    { 
                        _currtalk.recvdata = p;
                        _currtalk.recvinfo = pi;
                        if (p.IsErrorResponse())
                        {   _currtalk.Stage = TalkStage.DoneFail;
                            ErrorLog("talk","error");
                            CommLog("E " + p.LogString() );
                        } else
                        {   
                            if (!Packet.Compare(_currtalk.senddata,_currtalk.recvdata))
                            {   _currtalk.Stage = TalkStage.DoneOk;
                                CommLog("R " + p.LogString() );
                                Log("Responce", p.LogString(true));
                                //Log("", p.LogString(true));
                                isprocesspacket =true;// <======================================================
                            } else
                            {   _currtalk.Stage = TalkStage.DoneFail;
                                ErrorLog("Wrong response " , p.LogString() );
                                CommLog("E " + p.LogString() );
                            }
                        }
                    } else
                    {
                        ErrorLog("Unexpected response " , p.LogString() );
                        CommLog("E " + p.LogString() );
                    }
                } else
                if (pi.type == CmdType.Read)
                { 
                    if ((_currtalk.Stage == TalkStage.Waiting) && (_currtalk.senddata.Cmd == p.Cmd))
                    { 
                        _currtalk.recvdata = p;
                        _currtalk.recvinfo = pi;
                        _currtalk.Stage = TalkStage.DoneOk;
                        CommLog( "R " + p.LogString() );
                        Log("Read", p.LogString(true));

                        isprocesspacket =true;
                    } else
                    {
                        ErrorLog("Unexpected response " ,p.LogString() );
                        CommLog("E " + p.LogString() );
                    }

                    
                } else
                {   Debug.Assert(false,"unsupported command");
                    CommLog("? " + p.LogString() );
                }



                if (_currtalk.IsDone)
                {   SBTalk nextinqueue =  (_talkstomake.Count==0) ? null : _talkstomake.Dequeue();
                    MakeATalkUnsafe(nextinqueue);
                }



            }

            if (!isprocesspacket)
                return;

            if (pi.characteristic==null)
                return;




            //todo: check for protocol errors

            ISBPacketProcessor pp = pi.characteristic as ISBPacketProcessor;
            if (pp!=null)
                pp.ProcessPacket(p);



        }

        
        [Conditional("DEBUG")]
        void CommLog(string s)
        {
            Debug.WriteLine(">>> "+s);

        }
        





        void ErrorLog(string a,string s)
        {
            Log(a,s);
        }

        void Log(string a, string s)
        {
            BaseConnection.DebugLogEx(_log, a, s);
        }




        void MakeATalkUnsafe(SBTalk t)
        { 
            if (t==null)
                return;

            //Debug.WriteLine("@@@ MakeATalk " + t.senddata.ToString());

            Debug.Assert(_currtalk.IsDone);

            _currtalk = t;
            CommLog("W "+_currtalk.senddata.LogString());
            Log("Write",_currtalk.senddata.LogString(true));
            _transport.WriteBytes(_currtalk.senddata.ToBytes());
            _currtalk.Stage = TalkStage.Waiting;
        }

        public SBTalk MakeATalk(Packet packettosend)
        {
            SBTalk t;
            t = new SBTalk(packettosend, GetInfo(packettosend.Cmd));
            t.Stage = TalkStage.InQueue;
            t.timeout_ms = DEFAULT_TIMEOUT_MS;
            t.starttime = DateTime.Now;

            lock (this)
            {
                if (_currtalk.Stage == TalkStage.Waiting)
                {   _talkstomake.Enqueue(t);
                    //Debug.WriteLine("@@@ MakeATalk Enqueue " + packettosend.ToString());
                } else
                {   MakeATalkUnsafe(t);
                }

            }

            return t;

        }









    }





}
