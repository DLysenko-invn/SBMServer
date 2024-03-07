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
        void WatcherStarted<T>(T sender) where T : class;

        

    }





    class DEVWatcher: IParentWatcher, IWatcher
    {
        ILog _log;
        IPacketSender _transport;
        IServerStatus _status;
        IWatcher[] _watchers; 
        IDeviceAliasTable _aliastab;

        public DEVWatcher(ILog log, IServerStatus status, IPacketSender transport,IDeviceAliasTable aliastab)
        {
            _log = log;
            _status = status;
            _transport = transport;
            _aliastab = aliastab;

            BLEWatcher ble = new BLEWatcher(this);
            USBWatcher usb = new USBWatcher(this);
            //IWatcher usb = new DummyWatcher();

            _watchers = new IWatcher[]{ ble, usb };

        }


        public bool IsStopped
        { 
            get 
            { 
                foreach(IWatcher w in _watchers)
                    if (!w.IsStopped)
                        return false;
                return true; 
            }     
        }

        public void Stop()
        { 
            foreach(IWatcher w in _watchers)
                if (!w.IsStopped)
                    w.Stop();
        }

        public void Start()
        {
            if (!IsStopped)    
                return;

            _status.DeviceListClear();

            foreach(IWatcher w in _watchers)
                w.Start();

        }


        public ILog Log
        {   get { return _log;}
        }


        



        public void WatcherDeviceFound<T>(T dev) where T : class
        {
            //Debug.Assert((dev is BLEDeviceInfo) || (dev is USBDeviceInfo));

            IDeviceInfo d = dev as IDeviceInfo;
    
            Debug.Assert(d!=null);

            _aliastab.RememberAlias(d);
            _status.DeviceFound(d);

            _transport.SendPacket(PM.DeviceFound(dev));

        }




        public void WatcherStopped<T>(T sender) where T : class
        {
            //Debug.Assert((sender is BLEWatcher) || (sender is USBWatcher));

            if (IsStopped)
            { 
                _status.IsWatcherActive = false;
                _transport.SendPacket(PM.Indication(PacketOpCode.watcher_stopped));
            }

        }

        public void WatcherStarted<T>(T sender) where T : class
        {

            bool isstarted = false;
            foreach(IWatcher w in _watchers)
                if (!w.IsStopped)
                    isstarted = true;

            if (isstarted)
                _status.IsWatcherActive = true;

        }



    }
}
