using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using BLE2TCP;

namespace SBMGUI
{
    public class AppCore:IDisposable
    {

        const string CFGFILENAME = "appconfig.json";

        ConsoleLog _log;
        AppSettings _cfg;
        Server _server;
        AppCoreStatus _status;
        Core _core;


        public AppCore(ConsoleLog log)
        {
            _log = log;          
            _cfg = new AppSettings(CFGFILENAME);
            _status = new AppCoreStatus();
            _status.Port = _cfg.Data.Port;
        }

        public AppCoreStatus Status
        {
            get { return _status;}
        }

        public void Dispose()
        {
            ServerStop();
        }

        public void ServerStart()
        {
            ServerStop();

            SocketServer t = new SocketServer(_log,_status);
            _core = new Core(_log,_status,t);
            _server = new Server(_log,_status,t,t,_core);

            _server.StartAsNewThread();
        }


        public void ServerStop()
        {
            if (_server == null)
                return;

            _server.Stop();
            _server = null;

            _core.Stop();
            _core = null;

        }


        public void StartWatcher()
        {
            _core.Watcher.Start();
        }

        public void OnMainWindowLoaded()
        {
            ServerStart();
        }
    }



    public class AppCoreStatus : INotifyPropertyChanged , IServerStatus
    {
        int _port;
        string _ip;
        int _connectionscount;
        ulong _rxbytes;
        ulong _txbytes;
        List<IDeviceInfo> _devices = new List<IDeviceInfo>();
        IDeviceInfo[] _devarray=new IDeviceInfo[]{};


        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e) 
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if(handler != null)
            {   handler(this, e);
            }
        }

        protected void SetPropertyField<T>(string propertyName, ref T field, T newValue) 
        {
            if(!EqualityComparer<T>.Default.Equals(field, newValue)) 
            {   field = newValue;
                OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            }
        }

        public int Port 
        {
            get { return _port; }
            set 
            {   if (value!=_port)
                SetPropertyField("Port", ref _port, value); 
            }
        }
        public string IP 
        {
            get { return _ip; }
            set 
            {   if (value!=_ip)
                    SetPropertyField("IP", ref _ip, value); 
            }
        }
        public int ConnectionsCount 
        {
            get { return _connectionscount; }
            set 
            {   if (value!=_connectionscount)
                    SetPropertyField("ConnectionsCount", ref _connectionscount, value); 
            }
        }

        public ulong RXBytes 
        {
            get { return _rxbytes; }
        }
        public ulong TXBytes 
        {
            get { return _txbytes; }
        }

        public void IncRX(int incval)
        {
            if (incval==IServerStatus.RESET)
            {   SetPropertyField("RXBytes", ref _rxbytes, (ulong)0); 
            } else
            if (incval!=0)
                SetPropertyField("RXBytes", ref _rxbytes, _rxbytes+(uint)incval); 
        }


        public void IncTX(int incval)
        {
            if (incval==IServerStatus.RESET)
            {   SetPropertyField("TXBytes", ref _txbytes, (ulong)0); 
            } else
            if (incval!=0)
                SetPropertyField("TXBytes", ref _txbytes, _txbytes+(uint)incval); 
        }

        public void DeviceFound(IDeviceInfo devinfo)
        {
            if (devinfo==null)
                return;

            if (!devinfo.IsTDK)
                return;

            _devices.Add(devinfo);
            Devices = _devices.ToArray();
        }

        public void DeviceListClear()
        {
            _devices = new List<IDeviceInfo>();
            Devices = _devices.ToArray();
        }

        public IDeviceInfo[] Devices 
        {
            get {  return _devarray; }
            private set 
            {   SetPropertyField("Devices", ref _devarray, value);
            }
        }
        

        public event PropertyChangedEventHandler PropertyChanged;

    }





}
