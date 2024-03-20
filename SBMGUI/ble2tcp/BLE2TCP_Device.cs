using BLE2TCP.BLEEMU;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;











namespace BLE2TCP
{











    class ConnectionFactory
    {

        static bool IsBluetooth(string id)
        { 
            return id.ToLower().Contains("bluetooth");
        }
        static bool IsSerialSB(string id)
        { 

            return id.ToLower().Contains("usb#");
        }
        static bool IsSerialDP(string id)
        { 
            return id.Contains("FTDIBUS");
        }


        public static IConnection Create(ILog log, string devid, BLECallbackProcessor proc)
        { 
        
            if (IsBluetooth(devid))
                return new BLEConnection(log, devid, proc);

            if (IsSerialSB(devid))
                return new SBUSBConnection(log, devid, proc);    

            if (IsSerialDP(devid))
                return new DPUSBConnection(log, devid, proc);

            return null;

        }



    }






    enum BLECallbackAction
    {
        None,
        Notification,
        Connected,
        Disconnected,



    }

    interface BLECallbackProcessor
    {
        void BLECallback(string device_uuid, BLECallbackAction operation, string service_uuid, string characteristic_uuid, byte[] data);

    }


    interface IConnection :  IDisposable
    {
        bool Connect();
        bool IsConnected { get; }
        Task<bool> Subscribe(string service_uuid, string characteristic_uuid);
        Task<bool> Unsubscribe(string service_uuid, string characteristic_uuid);
        Task<byte[]> Read(string service_uuid, string characteristic_uuid);
        Task<bool> Write(string service_uuid, string characteristic_uuid, byte[] data);

        Task<bool> Disconnect();
        Task<bool> Pair();
        Task<bool> Unpair();

    }









    class BaseConnection
    {
        protected bool _isconnected;
        protected ILog _log;
        protected BLECallbackProcessor _proc;
        protected string _uuid;


        protected bool Init(ILog log, string devid, BLECallbackProcessor proc)
        {
            Debug.Assert(log != null);
            _log = log;

            Debug.Assert(proc != null);
            _proc = proc;

            _isconnected = false;

            _uuid = devid;
            if (string.IsNullOrEmpty(_uuid))
            {
                Debug.Assert(false);
                log.LogError("Device Id is not provided");
                return false;
            }

            return true;

        }



        static string fmtid(string id)
        {
            if (id == null)
                return string.Empty;
            if ((id.Length > 9) && (id[8] == '-'))
                return id.Substring(0, 8);
            return id;
        }

        protected string BytesToString(byte[] data)
        {
            if (data == null)
                return string.Empty;
            StringBuilder hex = new StringBuilder();
            int n = 0;
            foreach (byte b in data)
            {
                n++;
                if (n > 10)
                {
                    hex.Append("...");
                    break;
                }
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();

        }

        public bool IsConnected
        {
            get { return _isconnected; }
        }

        static public void DebugLogEx(ILog _log, string activity, string s = null, string c = null, string suffix = null)
        {
            _log.LogLine(string.Format("{0} {1}  {2} {3}", activity.PadRight(20), fmtid(s), fmtid(c), suffix ?? ""));
        }

        protected void DebugLog(string activity, string s = null, string c = null, byte[] data = null)
        {
            DebugLogEx(_log, activity, s, c, BytesToString(data));
        }

        protected void DebugLogError(string activity, string errormsg, string s = null, string c = null, byte[] data = null)
        {
            _log.LogError(string.Format("ERROR {0} {1} {2}  {3} {4}", activity, errormsg, fmtid(s), fmtid(c), BytesToString(data)));
        }




    }






}
