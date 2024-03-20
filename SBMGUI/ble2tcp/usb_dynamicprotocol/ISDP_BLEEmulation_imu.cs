using DynamicProtocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BLE2TCP.BLEEMU
{




    class ISDP_FakeServiceIMU : FakeServiceBase
    {

        override public string UUID { get { return "00000100-2000-1000-8000-cec278b6b50a"; } }

        const int SID = ISDP.SID_RAW_ACCELEROMETER;

        IFakeCharacteristic _rw;
        IFakeCharacteristic _n;

        public ISDP_FakeServiceIMU(BLECallbackProcessor proc, IDPTalkMaker transport)
        {
            
            FakeCharacteristicRW rw = new FakeCharacteristicRW(this, transport,SID);
            _rw = AddChar(rw);
            _n = AddChar(new FakeCharacteristicNotify(this, proc,rw));
        }

        public IFakeCharacteristic RW { get { return _rw; } }
        public IFakeCharacteristic N { get { return _n; } }


        public override IFakeCharacteristic NotifyCharacteristic
        {   get{ return _n; }
        }


        class FakeCharacteristicNotify : FakeCharacteristicBase, IDPPacketProcessor
        {

            override public string UUID { get { return "00000101-2000-1000-8000-cec278b6b50a"; } }
            ISDP.CmdSensorData _decoder; 
            FakeCharacteristicRW _rw;
            const double I16_RANGE = 32767.0;

            public FakeCharacteristicNotify(IFakeService parent, BLECallbackProcessor proc, FakeCharacteristicRW rw) : base(parent,proc)
            {
                Debug.Assert(rw!=null);
                _rw = rw;

                _decoder = new ISDP.CmdSensorDataRawAccGyro();

            }

            override public bool Subscribe()
            {
                return _rw.SetEnabled(true);
            }

            override public bool Unsubscribe()
            {
                return _rw.SetEnabled(false);
            }

            Int16 ConvertI16(double a)
            {   return _rw.AccelODRHz==0 ? (Int16)0 : (Int16)(a*I16_RANGE/_rw.AccelODRHz);
            }

            public bool ProcessCommand(ISDPCmd cmd)
            {

                if (cmd==null)
                    return false;
            
               ISDP.Data d = _decoder.Decode(cmd);

               if (d==null)
                    return false;

                UInt64 ts=(UInt64)d.timestamp;
                Int16 x=(Int16)d.ix;// ConvertI16(d.x);
                Int16 y=(Int16)d.iy;
                Int16 z=(Int16)d.iz;




                byte[] data = BLEProtocolEncode.IMUEvent((UInt32)ts, x,y,z);
                Notify(data);

                return true;
            }





        }


        class FakeCharacteristicRW : FakeCharacteristicBase
        {
            override public string UUID { get { return "00000102-2000-1000-8000-cec278b6b50a"; } }

            IDPTalkMaker _transport;
            bool _isenabled = false;
            int _odr=0;
            int _fsrenum=0;
            int _accel_sensor_id;



            /* GYRO
            const int DEFAULT_FSR_DPS = 0;
            Dictionary<int,int>  _fsrenum2dps = new Dictionary<int, int>(){ {0,16}, {1,31}, {2,62}, {3,125}, {4,250},  {5,500}, {6,1000}, {7, 2000}, {8, 4000} };
            int fsrenum2dps(int fsrenum)
            {   return  _fsrenum2dps.ContainsKey(fsrenum) ? _fsrenum2dps[fsrenum] : DEFAULT_FSR_DPS;
            }
            */

            // ACCEL
            const int DEFAULT_FSR_G = 0;
            Dictionary<int,int>  _fsrenum2g = new Dictionary<int, int>(){ {0,2}, {1,4}, {2,8}, {3,16}, {4,32} };
            int fsrenum2g(int fsrenum)
            {   
                return  _fsrenum2g.ContainsKey(fsrenum) ? _fsrenum2g[fsrenum] : DEFAULT_FSR_G;
            }

            public int AccelODRHz
            {   get
                {
                    return _odr; 
                }
            }

            public FakeCharacteristicRW(IFakeService parent, IDPTalkMaker transport,int accsid) : base(parent)
            {
                Debug.Assert( accsid!=ISDP.SID_NULL);
                Debug.Assert( transport!=null );

                _transport = transport;
                _accel_sensor_id = accsid;

            
                 //todo: read current ORD,FSR from the device

            }

            override public byte[] Read()
            {
                Get();
                return BLEProtocolEncode.IMURead(_isenabled,(UInt16)_odr,(byte)_fsrenum);
            }


            override public bool Write(byte[] data)
            {
                BLEProtocolDecode.IMUData(data, out int accodr, out int accfsrenum);
                return Set(accodr, accfsrenum);
            }



            bool Get()
            {
                //todo: IMPLEMENT
                return true;
            }


       
            bool Set(int newaccodr, int newaccfsrenum)
            {

 


                { 
                    ISDPCmd c = ISDP.SetConfigODR(_accel_sensor_id,newaccodr);
                    DPTalk t = _transport.MakeATalk(c.Frame);
                    t.WaitTillDone(USBSmartMotionAsBLE.DEFAULT_TIMEOUT_MS);
                    if (!t.IsDoneOK)
                        return false;  
                    _odr = (int)newaccodr;
                }

                { 
                    ISDPCmd c = ISDP.SetConfigFSR(_accel_sensor_id,fsrenum2g(newaccfsrenum));
                    DPTalk t = _transport.MakeATalk(c.Frame);
                    t.WaitTillDone(USBSmartMotionAsBLE.DEFAULT_TIMEOUT_MS);
                    if (!t.IsDoneOK)
                        return false;  
                    _fsrenum = (int)newaccfsrenum;
                }


                return true;


            }

            public bool SetEnabled(bool newisenable)
            {
                ISDPCmd c =  (bool)newisenable ? ISDP.Start(_accel_sensor_id) : ISDP.Stop(_accel_sensor_id) ;
                DPTalk t = _transport.MakeATalk(c.Frame);
                t.WaitTillDone(USBSmartMotionAsBLE.DEFAULT_TIMEOUT_MS);
                return t.IsDoneOK;
            }



        }




    }





}
