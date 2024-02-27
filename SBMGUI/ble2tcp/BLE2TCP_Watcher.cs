using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BLE2TCP
{



    interface IWatcher
    {
        bool IsStopped { get;}
        void Stop();
        void Start();
        

    }


    interface IParentWatcher
    {
        ILog Log{ get; }

        void WatcherDeviceFound<T>(T dev) where T : class;
        void WatcherStopped<T>(T sender) where T : class;

    }



    class DEVWatcher: IParentWatcher, IWatcher
    {
        ILog _log;
        IPacketSender _transport;
        BLEWatcher _ble;
        //USBWatcher _usb;
        IWatcher _usb;

        public DEVWatcher(ILog log, IPacketSender transport)
        {
            _log = log;
            _transport = transport;
            _ble = new BLEWatcher(this);
            //_usb = new USBWatcher(this);
            _usb = new DummyWatcher();


        }


        public bool IsStopped
        { 
            get { return ((_ble.IsStopped) && (_usb.IsStopped)); }     
        }

        public void Stop()
        { 
            _ble.Stop();
            _usb.Stop();
        }

        public void Start()
        {
            _ble.Start();
            _usb.Start();

        }


        public ILog Log
        {   get { return _log;}
        }

        public void WatcherDeviceFound<T>(T dev) where T : class
        {
            //Debug.Assert((dev is BLEDeviceInfo) || (dev is USBDeviceInfo));
            _transport.SendPacket(PM.DeviceFound(dev));

        }




        public void WatcherStopped<T>(T sender) where T : class
        {
            //Debug.Assert((sender is BLEWatcher) || (sender is USBWatcher));

            if (IsStopped)
                _transport.SendPacket(PM.Indication(PacketOpCode.watcher_stopped));

        }
    }
}
