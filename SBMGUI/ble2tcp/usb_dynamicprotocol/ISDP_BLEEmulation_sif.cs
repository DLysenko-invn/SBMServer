using System;
using System.Collections.Generic;
using System.Text;

namespace BLE2TCP.BLEEMU
{

    class ISDP_FakeServiceSIF : FakeServiceBase
    {

        override public string UUID { get { return "00001500-2000-1000-8000-cec278b6b50a"; } }

        IFakeCharacteristic _rw;
        IFakeCharacteristic _n;

        public ISDP_FakeServiceSIF(BLECallbackProcessor proc, ISBTalkMaker transport)
        {
            FakeCharacteristicSIFRW rw = new FakeCharacteristicSIFRW(this, transport);
            _rw = AddChar(rw);
            _n = AddChar(new FakeCharacteristicN(this, proc,rw));
        }

        public IFakeCharacteristic RW { get { return _rw; } }
        public IFakeCharacteristic N  { get { return _n;  } }





        class FakeCharacteristicN : FakeCharacteristicBase,ISBPacketProcessor
        {

            override public string UUID { get { return "00001501-2000-1000-8000-cec278b6b50a"; } }
            FakeCharacteristicSIFRW _rw;

            public FakeCharacteristicN(IFakeService parent, BLECallbackProcessor proc, FakeCharacteristicSIFRW rw) : base(parent)
            {
                _proc = proc;
                _rw = rw;
            }

            override public bool Subscribe()
            {
                return _rw.Set(SIFOperation.Enable);
            }

            override public bool Unsubscribe()
            {
                return _rw.Set(SIFOperation.Disable);
            }


            public void ProcessPacket(Packet p)
            {   
                /*
                USBProtocolDecode.SIFEvent(p, out UInt64 ts, out byte label);
                byte[] data = BLEProtocolEncode.SIFEvent((UInt32)ts, label);
                Notify(data);
                */
            }

        }



        class FakeCharacteristicRW : FakeCharacteristicBase
        {
            override public string UUID { get { return "00001502-2000-1000-8000-cec278b6b50a"; } }

            ISBTalkMaker _transport;
            bool _isenabled = false;
            bool _isinitialized = false;



            public FakeCharacteristicRW(IFakeService parent, ISBTalkMaker transport) : base(parent)
            {
                _transport = transport;
            

            }

            override public byte[] Read()
            {
                Get();
                return BLEProtocolEncode.SIFRead(_isenabled);
            }


            override public bool Write(byte[] data)
            {
                return Set(SIFOperation.Config, data);

            }

            bool Get()
            {/*
                SBTalk t = _transport.MakeATalk(USBProtocolEncode.SIFDataRead());
                t.WaitTillDone(USBPebbleAsBLE.DEFAULT_TIMEOUT_MS);
                if (!t.IsDoneOK)
                    return false;

                bool rc = USBProtocolDecode.SIFDataRead(t.recvdata, out bool currisenable);
                if (!rc)
                    return false;
                _isenabled = currisenable;
*/
                return true;
            }


       
            public bool Set(SIFOperation cmd, byte[] datachunk=null)
            {
/*
                if (!_isinitialized)
                {
                    _isinitialized = true;
                    Set(SIFOperation.Disable, null);
                    _isenabled = false;
                }
                SBTalk tt = _transport.MakeATalk(USBProtocolEncode.SIFDataWrite(cmd, datachunk));
                tt.WaitTillDone(USBPebbleAsBLE.DEFAULT_TIMEOUT_MS);
                if (tt.IsDoneOK)
                {
                    if ((cmd == SIFOperation.Enable) || (cmd == SIFOperation.Disable))
                        _isenabled = (cmd == SIFOperation.Enable);
                    return true;
                }
*/
                return false;
            }



        }







    }



}
