using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BLE2TCP
{
    class SocketServer : ITransport, IPacketSender
    {
        int _port;
        ILog _log;
        Socket _client;
        static byte[] NOBYTES = new byte[] { };
        Socket _listener;
        IPAddress _address;
        Counter _counter;
        Sender _sender;


        class Counter
        {
            ILog _log;
            ulong _sendcounter, _recvcounter;
            bool _isdirty;
            DateTime _time;
            public Counter(ILog log)
            {
                _log = log;
                Reset();
                _isdirty = false;
            }

            public void Reset()
            {
                _sendcounter = 0;
                _recvcounter = 0;
                _isdirty = true;
                _time = new DateTime(1970,1,1);
            }


            public void Inc(bool issend, ulong count)
            {
                if (issend)
                {   _sendcounter += count;
                }  else
                {   _recvcounter += count;
                }
                _isdirty = true;
                WriteLog();
            }


            public void WriteLog()
            {
                if (!_isdirty)
                    return;
                if ((DateTime.Now - _time).TotalSeconds < 10)
                    return;

                //_log.LogLine( string.Format("Transport {0}/{1}", _sendcounter, _recvcounter) );
                _isdirty = false;
                _time = DateTime.Now;
            }




        }

        class Sender
        {
            Thread _thread;
            Socket _client;
            Counter _counter;
            ConcurrentQueue<byte[]> _data;
            EventWaitHandle _havedata;
            EventWaitHandle _start;
            EventWaitHandle _stop;
            EventWaitHandle _close;

            public Sender()
            {
                _thread = null;
                _client = null;
            }

            public void Send(byte[] bytes)
            {
                if (_data == null)
                    return;
                _data.Enqueue(bytes);
                _havedata.Set();
            }

            void SendProc()
            {
                WaitHandle[] waithandles = new WaitHandle[] { _close, _havedata };
                _data = new ConcurrentQueue<byte[]>();
                _start.Set();

                while (true)
                {
                    int index = WaitHandle.WaitAny(waithandles);

                    if (index == 0)
                        break;

                    if (_client == null)
                        continue;

                    while (_data.TryDequeue(out byte[] data))
                    {
                        try
                        {
                            int n = _client.Send(data);
                            _counter.Inc(true, (uint)n);
                            Debug.Assert(n == data.Length);
                        }
                        catch
                        {
                            _close.Set();
                            break;
                        }
                    }

                }

                _data = null;
                _stop.Set();

            }

            public void Start(Socket client,Counter counter)
            {
                _close = new AutoResetEvent(false);
                _havedata = new AutoResetEvent(false);
                _start = new AutoResetEvent(false);
                _stop = new AutoResetEvent(false);
                _counter = counter;
                _client = client;
                _thread = new Thread(SendProc);
                _thread.Start();
                _start.WaitOne();
            }

            public void Stop()
            {
                _close.Set();
                _stop.WaitOne();
            }

            public bool IsStopped()
            {
                return _data == null;
            }


        }

        public SocketServer(ILog log,int port)
        { 
            _log=log;
            _port = port;
            _client = null;
            _listener = null;
            _address = null;
            _counter = new Counter(log);
            _sender = null;

        }


        public bool SendPacket(PacketBase packet)
        {
            byte[]  data = packet.ToBytes();
            return WriteBytes( data ) == data.Length;
        }




        public bool IsConnected
        {
            get 
            {
                return (_client!=null);
            }
        }

        public byte[] ReadBytes(int size)
        {

            if (_client == null)
                return NOBYTES;
            byte[] data = new byte[size];
            int n;
            try
            {   n = _client.Receive(data);
                _counter.Inc(false,(uint)n);
                if ((n != data.Length) && (n!=0))
                {   _log.LogError(string.Format("Socket receive error {0} {1}", size, n));
                }
            }
            catch
            {
                CloseConnection();
                n = 0;
            }
            
            if (n != size)
            {
                CloseConnection();
                return NOBYTES;
            }

            return  data;
        }



        public void CloseConnection()
        {
            if (_client == null)
                return;
            Socket c = _client;
            _client = null;
            c.Shutdown(SocketShutdown.Both);
            c.Close();
        }

        public bool WaitForConnection()
        {

            Debug.Assert(_listener != null);
            bool rc;

            try
            {   _log.LogLine("Waiting for a connection @" + _address.ToString() + ":" + _port.ToString() + " ...");
                _client = _listener.Accept();
                _log.LogLine("Connected");
                rc = true;
            }
            catch (Exception e)
            {   _log.LogError("Socket exception : " + e.ToString());
                rc = false;
            }

            _counter.Reset();
            _sender = new Sender();
            _sender.Start(_client, _counter);

            return rc;

        }


        public bool Start()
        {

            IPHostEntry hostinfo = Dns.GetHostEntry("localhost");
            foreach (IPAddress address in hostinfo.AddressList)
            {
                byte[] addr = address.GetAddressBytes();
                if (addr[0] == 127 && addr[1] == 0 && addr[2] == 0 && addr[3] == 1)
                {
                    _address = address;
                    break;
                }
            }

            bool rc = false;

            if (_address != null)
            {
                IPEndPoint localendpoint = new IPEndPoint(_address, _port);
                _listener = new Socket(_address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                rc = true;
                try
                {
                    _listener.Bind(localendpoint);
                    _listener.Listen(10);

                }
                catch (Exception e)
                {
                    _log.LogLine("Socket exception : " + e.ToString());
                    rc = false;
                }
            }



            return rc;

        }

        public void Stop()
        {

            CloseConnection();

            if (_listener == null)
                return;
            Socket c = _listener;
            _listener = null;
            try
            {   c.Shutdown(SocketShutdown.Both);
                c.Close();
            }
            catch
            {    
            }
            if (_sender!=null)
            {   _sender.Stop();
                _sender = null;
            }
            
        }




        public int WriteBytes(byte[] data)
        {
            if (_sender == null)
                return 0;
            if (_sender.IsStopped())
            {   Stop();
                return 0;
            }
            _sender.Send(data);
            return data.Length;

            /*
            if (_client == null)
                return 0;
            int n;
            try 
            {
                n = _client.Send(data);
                _counter.Inc(true, (uint)n);
                if (n != data.Length)
                {   _log.LogError(string.Format("Socket send error {0} {1}", data.Length, n));
                }
            }
            catch
            {
                Stop();
                n = 0;
            }
            return n;
            */
        }


        public void OnTimer()
        {
            _counter.WriteLog();
        }


    }












}
