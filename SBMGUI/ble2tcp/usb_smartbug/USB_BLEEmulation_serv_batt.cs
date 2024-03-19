using System;
using System.Collections.Generic;
using System.Text;

namespace BLE2TCP.BLEEMU
{

    // todo: implement notify

    class FakeServiceBATT : FakeServiceBase
    {

        override public string UUID { get { return "0000180f-0000-1000-8000-00805f9b34fb"; } }

        IFakeCharacteristic _c;

        public FakeServiceBATT(BLECallbackProcessor proc)
        {
            _c = AddChar(new FakeCharacteristic(this));
        }

        public IFakeCharacteristic C { get { return _c; } }


        class FakeCharacteristic : FakeCharacteristicBase
        {
            override public string UUID { get { return "00002a19-0000-1000-8000-00805f9b34fb"; } }


            public FakeCharacteristic(IFakeService parent) : base(parent)
            {

            }

            override public byte[] Read()
            {
                return BLEProtocolEncode.BattStatus();
            }
            override public bool Write(byte[] data)
            {
                return false;
            }
        }


    }



}
