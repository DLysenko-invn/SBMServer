using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BLE2TCP.BLEEMU
{


    interface IFakeService
    {
        string UUID { get; }
        bool Subscribe(string uuid);
        bool Unsubscribe(string uuid);
        byte[] Read(string uuid);
        bool Write(string uuid, byte[] data);
        IFakeCharacteristic NotifyCharacteristic {get;}

    }

    interface IFakeCharacteristic
    {
        string UUID { get; }
        bool Subscribe();
        bool Unsubscribe();

        void Notify(byte[] data);

        byte[] Read();
        bool Write(byte[] data);

    }

    


    abstract class FakeCharacteristicBase : IFakeCharacteristic
    {
        
        protected const int NOTSET = -1;
        protected BLECallbackProcessor _proc = null;
        protected IFakeService _parent;

        abstract public string UUID { get; }

        public FakeCharacteristicBase(IFakeService parent)
        {
            Debug.Assert(this.UUID == this.UUID.ToLower());
            _parent = parent;
            _proc = null;
        }

        public FakeCharacteristicBase(IFakeService parent,BLECallbackProcessor proc):this(parent)
        {
            _proc = proc;
        }

        virtual public bool Subscribe()
        {
            return false;
        }

        virtual public bool Unsubscribe()
        {
            return false;
        }

        virtual public byte[] Read()
        {
            return null;
        }
        virtual public bool Write(byte[] data)
        {
            return false;
        }
        public void Notify(byte[] data)
        {
            if (_proc == null)
                return;

            _proc.BLECallback(null, BLECallbackAction.Notification, _parent.UUID, this.UUID, data);

        }


    }

    abstract class FakeServiceBase : IFakeService
    {
        protected Dictionary<string, IFakeCharacteristic> _chars = new Dictionary<string, IFakeCharacteristic>();
        abstract public string UUID { get; }


        public FakeServiceBase()
        {
            Debug.Assert(this.UUID == this.UUID.ToLower());
        }

        public virtual IFakeCharacteristic NotifyCharacteristic
        {
            get{    return null; }
        }



        IFakeCharacteristic Find(string uuid)
        {
            if (!_chars.ContainsKey(uuid))
                return null;
            return _chars[uuid];
        }

        public bool Subscribe(string uuid)
        {
            IFakeCharacteristic c = Find(uuid);
            return (c == null) ? false : c.Subscribe();
        }

        public bool Unsubscribe(string uuid)
        {
            IFakeCharacteristic c = Find(uuid);
            return (c == null) ? false : c.Unsubscribe();
        }

        protected IFakeCharacteristic AddChar(IFakeCharacteristic c)
        {
            Debug.Assert(!_chars.ContainsKey(c.UUID));
            _chars[c.UUID] = c;
            return c;
        }

        public byte[] Read(string uuid)
        {
            IFakeCharacteristic c = Find(uuid);
            return (c == null) ? null : c.Read();
        }
        public bool Write(string uuid, byte[] data)
        {
            IFakeCharacteristic c = Find(uuid);
            return (c == null) ? false : c.Write(data);
        }


    }





}
