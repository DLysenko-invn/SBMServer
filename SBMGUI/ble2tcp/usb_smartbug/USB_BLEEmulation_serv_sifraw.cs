using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BLE2TCP.BLEEMU
{



    class FakeServiceSIFRAW : FakeServiceBase
    {

        override public string UUID { get { return "00001600-2000-1000-8000-cec278b6b50a"; } }

        IFakeCharacteristic _rw;
        IFakeCharacteristic _n;

        public FakeServiceSIFRAW(BLECallbackProcessor proc, ITalkMaker transport)
        {

            FakeCharacteristicSIFRAW_RW rw = new FakeCharacteristicSIFRAW_RW(this, transport);
            _rw = AddChar(rw);
            _n = AddChar(new FakeCharacteristicSIFRAW_Notify(this, proc, rw));
        }

        public IFakeCharacteristic RW { get { return _rw; } }
        public IFakeCharacteristic N { get { return _n; } }
    }



    class FakeCharacteristicSIFRAW_Notify : FakeCharacteristicBase
    {

        override public string UUID { get { return "00001601-2000-1000-8000-cec278b6b50a"; } }
        FakeCharacteristicSIFRAW_RW _rw;

        public FakeCharacteristicSIFRAW_Notify(IFakeService parent, BLECallbackProcessor proc, FakeCharacteristicSIFRAW_RW rw) : base(parent)
        {
            _proc = proc;
            _rw = rw;
        }

        override public bool Subscribe()
        {
            return _rw.Set(true);
        }

        override public bool Unsubscribe()
        {
            return _rw.Set(false);
        }


        override public void ProcessPacket(Packet p)
        {
            USBProtocolDecode.SIFRAWEvent(p, out UInt64 ts, out UInt32 ax, out UInt32 ay, out UInt32 az);
            byte[] data = BLEProtocolEncode.SIFRAWEvent((UInt32)ts, ax,  ay,  az);
            Notify(data);
        }


    }




    class FakeCharacteristicSIFRAW_RW : FakeCharacteristicBase
    {
        override public string UUID { get { return "00001a02-2000-1000-8000-cec278b6b50a"; } }

        ITalkMaker _transport;
        bool? _isenabled;



        public FakeCharacteristicSIFRAW_RW(IFakeService parent, ITalkMaker transport) : base(parent)
        {
            _transport = transport;
            _isenabled = null;
        }

        override public byte[] Read()
        {
            return null;
        }
        override public bool Write(byte[] data)
        {
            return false;
        }

        bool Get()
        {
            Talk t = _transport.MakeATalk(USBProtocolEncode.SIFDataRead());
            t.WaitTillDone(USBPebbleAsBLE.DEFAULT_TIMEOUT_MS);
            if (!t.IsDoneOK)
                return false;

            bool rc = USBProtocolDecode.SIFRawDataRead(t.recvdata, out bool currisenable);
            if (!rc)
                return false;
            _isenabled = currisenable;
            return true;
        }


        public bool Set(bool isenabled)
        {
            if (_isenabled==null)
            {   Get();
            }

            if (_isenabled == null)
                return false;

            if (((bool)_isenabled) == isenabled)
                return true;

            Talk tt = _transport.MakeATalk(USBProtocolEncode.SIFRAWDataWrite(isenabled));
            tt.WaitTillDone(USBPebbleAsBLE.DEFAULT_TIMEOUT_MS);
            if (tt.IsDoneOK)
            {   _isenabled = isenabled;
                return true;
            }
            return false;
        }



    }


}
