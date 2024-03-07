using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace BLE2TCP.BLEEMU
{



    interface IUSBtoBLEProtocol
    {
        void Reset();
        void ProcessByte(byte b);
        uint MinimalRead { get; }

        bool IsTimeoutDetected();
        byte[] Read(string service_uuid, string characteristic_uuid);
        bool Write(string service_uuid, string characteristic_uuid, byte[] data);
        bool Subscribe(string service_uuid, string characteristic_uuid);
        bool Unsubscribe(string service_uuid, string characteristic_uuid);

    }

    interface IUSBTransport
    {
        bool WriteBytes(byte[] data);
    }

    class DummyProtocol : IUSBtoBLEProtocol
    {
        public static DummyProtocol STUB = new DummyProtocol();
        public uint MinimalRead
        {   get { return 1; }
        }
        public void ProcessByte(byte b)
        {
        }
        public void Reset()
        {
        }
        public bool IsTimeoutDetected()
        {   return false;
        }
        public byte[] Read(string service_uuid, string characteristic_uuid)
        {   return null;
        }
        public bool Write(string service_uuid, string characteristic_uuid, byte[] data)
        {   return true;
        }
        public bool Subscribe(string service_uuid, string characteristic_uuid)
        {   return true;
        }
        public bool Unsubscribe(string service_uuid, string characteristic_uuid)
        {   return true;
        }

    }










    class USBConnection : BaseConnection, IConnection, IUSBTransport, BLECallbackProcessor
    {

        SerialDevice _dev;
        DataReader _reader;
        DataWriter _writer;
        Thread _readthread;
        bool _iskeepreading;
        System.Timers.Timer _protocolmonitor;
        const int PROTOCOLCHECKTIME_MS = 3000;
        const uint BAUD = 460800;
        const uint READTIMEOUT_MS = 1000;
        const uint WRITETIMEOUT_MS = 1000;


        IUSBtoBLEProtocol _prot;

        public USBConnection(ILog log, string devid, BLECallbackProcessor proc)
        {
            if (!Init(log, devid, proc))
                return;

            _prot = new USBPebbleAsBLE(log, devid, this ,this);

            _dev = null;


        }


        public void BLECallback(string device_uuid, BLECallbackAction operation, string service_uuid, string characteristic_uuid, byte[] data)
        {
            _proc.BLECallback(_uuid, operation, service_uuid, characteristic_uuid, data);
        }



        public bool Connect()
        {
            Task<bool> t = Open();
            t.Wait();
            return t.Result;
        }

        public Task<bool> Disconnect()
        {
            return Task<bool>.Factory.StartNew(() => { Close(); return true; });
        }






        async Task<bool> Open()
        {

            Close();


            Debug.WriteLine("COM Connect..." );

            string id = _uuid;
            Debug.Assert(id != string.Empty);

            try
            {
                _dev = await SerialDevice.FromIdAsync(id).AsTask();
                if (_dev == null)
                {
                    DebugLogError("open","Serial device create error");
                    return false;
                }
            }
            catch (Exception e)
            {
                DebugLogError("open","Serial device create error: " + e.Message.ToString());
                return false;
            }

            Debug.WriteLine("Using " + _dev.PortName);

            try
            {
                this._dev.BaudRate = BAUD;
                this._dev.DataBits = 8;
                this._dev.StopBits = SerialStopBitCount.One;
                this._dev.Parity = SerialParity.None;
                this._dev.Handshake = SerialHandshake.None;
                this._dev.ReadTimeout = TimeSpan.FromMilliseconds(READTIMEOUT_MS);
                this._dev.WriteTimeout = TimeSpan.FromMilliseconds(WRITETIMEOUT_MS);
                this._dev.IsRequestToSendEnabled = true;
                this._dev.IsDataTerminalReadyEnabled = true;
                _writer = new DataWriter(this._dev.OutputStream);
                _reader = new DataReader(this._dev.InputStream);
            }
            catch (Exception e)
            {
                DebugLogError("open","Serial device configuration error: " + e.Message.ToString());
                Close();
                return false;
            }

            
            DebugLog("open", _dev.PortName + " @" + this._dev.BaudRate.ToString());

            Debug.Assert(_writer != null);
            Debug.Assert(_reader != null);

            _readthread = new Thread(this.ReadPort);
            _iskeepreading = true;
            _prot.Reset();
            _readthread.Start();

            _protocolmonitor = new System.Timers.Timer(PROTOCOLCHECKTIME_MS);
            _protocolmonitor.Elapsed += OnProtocolmonitor_Elapsed;
            _protocolmonitor.AutoReset = true;
            _protocolmonitor.Enabled = true;

            _isconnected = true;
            _proc.BLECallback(_uuid,  BLECallbackAction.Connected,null,null,null);

            // Debug.WriteLine("COM Connect OK" );

            return true;


        }



        private void OnProtocolmonitor_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_prot.IsTimeoutDetected())
            {
                DebugLogError("usb","Protocol timeout");

                //todo: may be resend the command?

                Close();
            }
        }



        private void ReadPort()
        {
            bool iserror = false;
            Debug.WriteLine("ReadPort START");
            while (_iskeepreading)
            {
                byte[] b;
                uint n = _prot.MinimalRead;

                //_debuglogfile?.WriteLine(  "?: " + n.ToString());
                //Debug.WriteLine("COM ? > " + n.ToString());

                try
                {
                    DataReaderLoadOperation t = _reader.LoadAsync(n);
                    t.AsTask().Wait();
                    //if (!t.AsTask().Wait(10000))
                    //{      
                    //}
                    //_reader.LoadAsync(1).AsTask().Wait();
                    uint m = _reader.UnconsumedBufferLength;
                    if (m > 0)
                    {
                        b = new byte[m];
                        // Debug.WriteLine("COM R > !1 "+m.ToString());
                        _reader.ReadBytes(b);
                        //Debug.WriteLine("COM R > !2");
                    }
                    else
                    {
                        //Debug.WriteLine("COM R > null");
                        continue;
                    }
                }
                catch (Exception e)
                {
                    if (_iskeepreading)
                    {   DebugLogError("read",e.Message);
                        iserror = true;
                    } else
                    {   // skip error report, closing in progress
                    }
                    _iskeepreading = false;
                    break;
                }

                //_debuglogfile?.WriteLine(  "r: " + Packet.ToHEXLine(b,false));
                //Debug.WriteLine("COM R > " + Packet.ToHEXLine(b,true) );
                foreach (byte d in b)
                    _prot.ProcessByte(d);
            }

            Debug.WriteLine("ReadPort STOP");

            if (iserror)
            {
                Close();
            }

        }









        void Close()
        {

            lock (this)
            {
                if (_dev == null)
                    return;

                Debug.WriteLine("COM Disconnect..." );
                string portname = _dev.PortName;
                _isconnected = false;
                _iskeepreading = false;
                if (_protocolmonitor != null)
                {   _protocolmonitor.Enabled = false;
                    _protocolmonitor.Dispose();
                    _protocolmonitor = null;

                }
                _prot = DummyProtocol.STUB;
                try
                {
                    if (_writer != null)
                    {
                        _writer.Dispose();
                        _writer = null;
                    }
                }
                catch { }
                try
                {
                    if (_reader != null)
                    {
                        _reader.Dispose();
                        _reader = null;
                    }
                }
                catch { }
                SerialDevice d = _dev;
                _dev = null;
                if (d != null)
                {
                    d.Dispose();
                }

                DebugLog("close", portname);
                _proc.BLECallback(_uuid, BLECallbackAction.Disconnected, null, null, null);
                Debug.WriteLine("COM Disconnect Done" );
            }

        }


        public bool WriteBytes(byte[] data)
        {

            if (_writer == null)
            {
                return false;
            }
            bool result = false;
            Exception lastexception = null;
            lock (this)
            {
                try
                {
                    foreach (byte b in data)
                    {
                        _writer.WriteByte(b);

                    }
                    _writer.StoreAsync().AsTask().Wait();
                    result = true;
                    //Debug.WriteLine("COM W > " + USBDeviceProtocolBase.Packet.ToHEXLine(data,true) );
                }
                catch (Exception e)
                {
                    lastexception = e;
                }

                //_writer.FlushAsync().AsTask().Wait();
            }
            if (lastexception != null)
            {
                DebugLogError("write",lastexception.Message);
                Close();
                return false;
            }

            return result;

        }


        public Task<bool> Pair()
        {
            return Task<bool>.Factory.StartNew(() => { return true; });
        }
        public Task<bool> Unpair()
        {
            return Task<bool>.Factory.StartNew(() => { return true; });
        }





        public void Dispose()
        {
            Close();
        }




        public  Task<byte[]> Read(string service_uuid, string characteristic_uuid)
        {
            return Task<byte[]>.Factory.StartNew(() => { return _prot.Read(service_uuid, characteristic_uuid); });
        }

        public Task<bool> Write(string service_uuid, string characteristic_uuid, byte[] data)
        {
            return Task<bool>.Factory.StartNew(() => { return _prot.Write(service_uuid, characteristic_uuid,data); });
        }


        public Task<bool> Subscribe(string service_uuid, string characteristic_uuid)
        {
            return Task<bool>.Factory.StartNew(() => { return _prot.Subscribe(service_uuid, characteristic_uuid); });
        }

        public Task<bool> Unsubscribe(string service_uuid, string characteristic_uuid)
        {
            return Task<bool>.Factory.StartNew(() => { return _prot.Unsubscribe(service_uuid, characteristic_uuid); });
        }

    }






}
