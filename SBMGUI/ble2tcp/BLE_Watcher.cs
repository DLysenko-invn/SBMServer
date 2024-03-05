using System;
using System.Collections.Generic;
using System.Text;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;





namespace BLE2TCP
{


    class BLEWatcher: IWatcher
    {
        IParentWatcher _parent;
        DeviceWatcher _watcher;

        public BLEWatcher(IParentWatcher parent)
        {
            _parent = parent;
        }

        public void  Start()
        {
            if (_watcher != null)
            {
                _parent.Log.LogLine("BLE Watcher already started");
                return;
            }

            _parent.WatcherStarted(this);

            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };
            string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

            _watcher = DeviceInformation.CreateWatcher(aqsAllBluetoothLEDevices, requestedProperties, DeviceInformationKind.AssociationEndpoint);
            _watcher.Added += OnWatcherAdded;
            _watcher.Updated += OnWatcherUpdated;
            _watcher.Removed += OnWatcherRemoved;
            _watcher.EnumerationCompleted += OnWatcherEnumerationCompleted;
            _watcher.Stopped += OnWatcherStopped;

            _watcher.Start();

            _parent.Log.LogLine("BLE Watcher started");


        }

        private void OnWatcherStopped(DeviceWatcher sender, object args)
        {
            Stop();
        }

        private void OnWatcherEnumerationCompleted(DeviceWatcher sender, object args)
        {
            Stop();
        }

        private void OnWatcherRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {

        }

        private void OnWatcherUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
        {

        }

        private void OnWatcherAdded(DeviceWatcher sender, DeviceInformation args)
        {
            BLEDeviceInfo i = new BLEDeviceInfo(args);
   
            _parent.WatcherDeviceFound(i);

        }

        public bool IsStopped
        {
            get
            {
                return (_watcher == null);
            }
        }

        public void Stop()
        {
            if (IsStopped)
                return;

            _watcher.Added -= OnWatcherAdded;
            _watcher.Updated -= OnWatcherUpdated;
            _watcher.Removed -= OnWatcherRemoved;
            _watcher.EnumerationCompleted -= OnWatcherEnumerationCompleted;
            _watcher.Stopped -= OnWatcherStopped;

            _watcher.Stop();
            _watcher = null;
            _parent.Log.LogLine("BLE Watcher destroyed");

            _parent.WatcherStopped(this);

        }


    }



}
