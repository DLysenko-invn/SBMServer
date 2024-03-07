using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;

namespace BLE2TCP
{












    

    class USBDeviceInfo:IDeviceInfo
    {

        const string VIDPIDMARK = "VID_1915&PID_520F";

        string _id,_portname,_name; 
        string _alias;

        public USBDeviceInfo(string id, string portname)
        {
            _id= id;
            _portname = portname;
            _name = (IsTDK ? "TDK " :"" ) + _portname;
            CreateAlias() ;
        }

        void CreateAlias()    
        {
            _alias = _portname;
        }



        [JsonPropertyName("id")]
        public string Id { get{ return _id;} }

        [JsonPropertyName("name")]
        public string Name  { get { return _name; } }

        [JsonPropertyName("is_paired")]
        public bool IsPaired => true;
        [JsonPropertyName("is_connected")]
        public bool IsConnected => false;
        [JsonPropertyName("is_connectable")]
        public bool IsConnectable => true;



        [JsonPropertyName("is_tdk")]
        public bool IsTDK 
        {   get
            {   return _id.ToUpper().Contains(VIDPIDMARK);
            }
        }


        [JsonIgnore]
        public string InterfaceName => "USB";

        [JsonPropertyName("alias")]
        public string Alias 
        {   get { return _alias; }
        }
    }

    class USBWatcher: IWatcher
    {
        IParentWatcher _parent;
        bool  _iscancel = true;
        bool _isrunning = false;
        Task _watcher = null;

        public USBWatcher(IParentWatcher parent)
        {
            _parent = parent;
        }

        public void  Start()
        {
            _iscancel = false;
            _watcher = GetPortNames();
            

        }


        async Task GetPortNames()
        {
            if (_isrunning)
                return;

            _parent.Log.LogLine("USB Watcher started");
            _isrunning = true;

            string aqs = SerialDevice.GetDeviceSelector();
            var devs = await DeviceInformation.FindAllAsync(aqs);
            foreach (var item in devs)
            {

                if (_iscancel)
                    break;

                SerialDevice s;
                try
                {
                    s = await SerialDevice.FromIdAsync(item.Id);
                    if (s == null)
                    {
                        continue;
                    }
                    _parent.WatcherDeviceFound( new USBDeviceInfo(item.Id, s.PortName) );

                }
                catch
                {
                    continue;
                }

                s.Dispose();


            }

            _parent.Log.LogLine("USB Watcher stopped");
            _isrunning = false;
            _parent.WatcherStopped(this);
        }








        public bool IsStopped
        {
            get
            {
                return (!_isrunning);
            }
        }

        public void Stop()
        {
            _iscancel = true;
        }


    }
    


}
