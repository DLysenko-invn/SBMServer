using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BLE2TCP.BLEEMU
{

    //warning: only ACCEl is supported    

    class FakeServiceIMU : FakeServiceBase
    {

        override public string UUID { get { return "00000100-2000-1000-8000-cec278b6b50a"; } }

        IFakeCharacteristic _rw;
        IFakeCharacteristic _n;

        public FakeServiceIMU(BLECallbackProcessor proc, ISBTalkMaker transport)
        {
            FakeCharacteristicIMURW rw = new FakeCharacteristicIMURW(this, transport);
            _rw = AddChar(rw);
            _n = AddChar(new FakeCharacteristicIMUNotify(this, proc,rw));
        }

        public IFakeCharacteristic RW { get { return _rw; } }
        public IFakeCharacteristic N { get { return _n; } }





    }



    class FakeCharacteristicIMUNotify : FakeCharacteristicBase, ISBPacketProcessor
    {

        override public string UUID { get { return "00000101-2000-1000-8000-cec278b6b50a"; } }
        FakeCharacteristicIMURW _rw;

        public FakeCharacteristicIMUNotify(IFakeService parent, BLECallbackProcessor proc, FakeCharacteristicIMURW rw) : base(parent)
        {
            _proc = proc;
            _rw = rw;
        }

        override public bool Subscribe()
        {
            return _rw.Set(true,null,null);
        }

        override public bool Unsubscribe()
        {
            return _rw.Set(false,null,null);
        }


        public void ProcessPacket(Packet p)
        {
            USBProtocolDecode.IMUEvent(p, out UInt64 ts, out Int16 x,out Int16 y,out Int16 z);
            byte[] data = BLEProtocolEncode.IMUEvent((UInt32)ts, x,y,z);
            Notify(data);
        }

    }


    class FakeCharacteristicIMURW : FakeCharacteristicBase
    {
        override public string UUID { get { return "00000102-2000-1000-8000-cec278b6b50a"; } }

        ISBTalkMaker _transport;
        bool _isenabled = false;
        bool _isinitialized = false;
        UInt16 _odr=0;
        byte _fsr=0;
        //fsr = Const.AccelFSRToValue((Const.AccelFSR)reader.GetByte());


        public FakeCharacteristicIMURW(IFakeService parent, ISBTalkMaker transport) : base(parent)
        {
            _transport = transport;
            

        }

        override public byte[] Read()
        {
            Get();
            return BLEProtocolEncode.IMURead(_isenabled,_odr,_fsr);
        }


        override public bool Write(byte[] data)
        {
            BLEProtocolDecode.IMUData(data, out int accodr, out int accfsrenum);
            return Set(null,accodr, accfsrenum);
        }

        bool Get()
        {
            SBTalk t = _transport.MakeATalk(USBProtocolEncode.IMUDataRead());
            t.WaitTillDone(USBPebbleAsBLE.DEFAULT_TIMEOUT_MS);
            if (!t.IsDoneOK)
                return false;

            bool rc = USBProtocolDecode.IMUDataRead(t.recvdata, out bool currisenable,out int curraccodr, out int curraccfsrenum);
            if (!rc)
                return false;
            _isenabled = currisenable;
            _odr = (UInt16) curraccodr;
            _fsr = (byte) curraccfsrenum;
            return true;
        }


       
        public bool Set(bool? newisenable, int? newaccodr, int? newaccfsrenum)
        {
            if (!_isinitialized)
            { 
                Get();
            }
        
            int odr = newaccodr==null ? _odr : (int)newaccodr;
            int fsr = newaccfsrenum==null ? _fsr : (int)newaccfsrenum;
            bool  isenable = newisenable == null ? _isenabled : (bool)newisenable;
            SBTalk tt = _transport.MakeATalk(USBProtocolEncode.IMUDataWrite(isenable, (UInt16)odr, (byte)fsr));
            tt.WaitTillDone(USBPebbleAsBLE.DEFAULT_TIMEOUT_MS);
            if (tt.IsDoneOK)
            {   _isenabled = isenable;
                _odr = (UInt16)odr;
                _fsr = (byte)fsr;
                return true;
            }
            return false;











        }



    }





}
