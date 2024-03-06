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
        ILog _log;
        IServerStatus _status;
        Socket _client;
        static byte[] NOBYTES = new byte[] { };
        Socket _listener;
        IPAddress _address;
        Sender _sender;









        class Sender
        {
            Thread _thread;
            Socket _client;
            ConcurrentQueue<byte[]> _data;
            EventWaitHandle _havedata;
            EventWaitHandle _start;
            EventWaitHandle _stop;
            EventWaitHandle _close;
            IServerStatus _status;

            public Sender()
            {
                _thread = null;
                _client = null;
                _status  = null;
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
                            _status.IncTX(n);
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

            public void Start(Socket client,IServerStatus status)
            {
                _close = new AutoResetEvent(false);
                _havedata = new AutoResetEvent(false);
                _start = new AutoResetEvent(false);
                _stop = new AutoResetEvent(false);
                _status  = status;
                _status.IncTX(IServerStatus.RESET);;
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

        public SocketServer(ILog log,IServerStatus status)
        { 
            _log=log;
            _status = status;
            _client = null;
            _listener = null;
            _address = null;
            _sender = null;

            _status.ConnectionsCount = IServerStatus.SERVER_STOPPED;

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
                _status.IncRX(n);
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

            _status.ConnectionsCount = 0;
            Socket c = _client;
            _client = null;
            c.Shutdown(SocketShutdown.Both);
            c.Close();
        }

        public bool WaitForConnection()
        {
            _status.ConnectionsCount = 0;

            if (_listener == null)
                Start();


            bool rc;
            try
            {   _log.LogLine("Waiting for a connection @" + _address.ToString() + ":" + _status.Port.ToString() + " ...");
                _client = _listener.Accept();
                _log.LogLine("Connected");
                rc = true;
            }
            catch (Exception e)
            {   
                if (_listener==null)
                {
                    // false accept while force closing listener socket 
                } else
                {   _log.LogError("Socket exception : " + e.ToString());
                }
                
                rc = false;
            }

            if (rc)
            {   _status.IncRX(IServerStatus.RESET);
                _status.IncTX(IServerStatus.RESET);
                _sender = new Sender();
                _sender.Start(_client, _status);
                _status.ConnectionsCount = 1;
            } else
            {   _status.ConnectionsCount = IServerStatus.SERVER_STOPPED;
            }


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
                IPEndPoint localendpoint = new IPEndPoint(_address, _status.Port);
                _listener = new Socket(_address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                rc = true;
                try
                {
                    _listener.Bind(localendpoint);
                    _listener.Listen(10);

                }
                catch (Exception e)
                {
                    _log.LogLine("Socket listener exception : " + e.ToString());
                    rc = false;
                }
            }

            _status.ConnectionsCount = rc ? 0 : IServerStatus.SERVER_STOPPED;

            return rc;

        }

        public void Stop()
        {
            _status.ConnectionsCount = IServerStatus.SERVER_STOPPED;

            CloseConnection();

            if (_listener == null)
                return;
            Socket c = _listener;
            _listener = null;
            try
            {   //c.Shutdown(SocketShutdown.Both);
                c.Close();
                    
            }
            catch (Exception e)
            {    _log.LogLine("Socket close exception : " + e.ToString());
            }
            if (_sender!=null)
            {   _sender.Stop();
                _sender = null;
            }

            _log.LogLine("Stopped");
            
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
            //todo: remove
            //_counter.WriteLog();
        }


    }












}
