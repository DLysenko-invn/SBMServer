using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;



namespace BLE2TCP
{






    class Server
    {
        ITransport _transport;
        IPacketParser _parser;
        IPacketSender _sender;
        PacketBuilder _builder;
        bool _isneedtoclose;
        System.Timers.Timer _timer;
        ILog _log;
        IServerStatus _status;


        public Server(ILog log,IServerStatus status,ITransport transport,IPacketSender sender,IPacketParser parser)
        { 
            _log=log;
            _status = status;
            _transport = transport;
            _parser = parser;
            _sender = sender;
            _builder = new PacketBuilder();
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += OnTimer; 
            _timer.AutoReset = true;
        }


        public void Stop()
        {
            _isneedtoclose = true;
            _transport.Stop();
            _timer.Enabled = false;
        }


        public void Start()
        {
            _isneedtoclose = false;
            _timer.Enabled = true;
            DoWork();
            
        }

        public Thread StartAsNewThread()
        { 
            Thread t =   new Thread(() => 
            {
                Thread.CurrentThread.IsBackground = true; 
                Start();

            });

            t.Start();

            return t;
        }




        private void OnTimer(object sender, System.Timers.ElapsedEventArgs e)
        {
            _transport.OnTimer();
        }

        void DoWork()
        {

            if (!_transport.Start())
                return;

            while (!_isneedtoclose)
            {
                _parser.ProcessPacketReset();

                _transport.WaitForConnection();

                while (_transport.IsConnected)
                {
                    Debug.Assert(_builder.MinimalReadSize != 0);
                    byte[] data = _transport.ReadBytes(_builder.MinimalReadSize);
                    //Debug.WriteLine("Got {0} bytes", data.Length);
                    if (data == null)
                        break;
                    foreach (byte b in data)
                        _builder.Add(b);
                    PacketBase p;
                    while ((p = _builder.Get()) != null)
                    {
                        PacketBase response = _parser.ProcessPacket(p);
                        if (response!=null)
                            _sender.SendPacket(response);

                    }
                }

                _transport.CloseConnection();



            }


            
        }

    }








    class PacketBuilder
    { 

        const int HEADERSIZE = 5;

        int _minimalread;
        int _pos;
        int _size;
        int _datapos;
        byte[] _data;
        Queue<PacketBase> _packets = new Queue<PacketBase>();

        
        public PacketBuilder()
        { 
            Reset();    
        }

        public void Reset()
        { 
            _minimalread = 2;
            _pos=0;
            _size = 0;
            _datapos = 0;
            _data = null;
                
        }


        public int MinimalReadSize
        {
            get { return _minimalread;}    
        }


        public void Add(byte[] data)
        { 
            foreach(byte b in data)    
                Add(b);
        }


        public void Add(byte b)
        {
            const int SIZE_SIZE = 2;

            switch(_pos)
            { 
                case 0:
                    _size = b;
                    Debug.Assert(_data==null);
                    Debug.Assert(_datapos==0);
                    _minimalread = 1;
                    _pos = 1;
                    break;
                case 1:
                    _size = (UInt16)((_size & (0x00FF)) | (((UInt16)b) << 8));
                    _minimalread = _size;
                    if (_size!=0)
                        _data = new byte[_size];
                    _pos = 2;
                    break;

               default:
                    Debug.Assert(_data!=null);
                    Debug.Assert(_datapos<_data.Length);
                    _data[_datapos++] = b;
                    _minimalread--;
                    if (_datapos == _data.Length)
                    {   _minimalread = SIZE_SIZE;
                        _pos = 0;
                        MakeNewPacket();
                    } else
                    {   _pos++;
                    }
                    break;

            }

        }


        void MakeNewPacket()
        {
            PacketBase p = new PacketBase(_data);
            Debug.Assert(p!=null);
            _size=0;
            _data=null;
            _datapos=0;

            lock(_packets)
            {    _packets.Enqueue(p);
            }
        }


        public PacketBase Get()
        { 
            if (_packets.Count==0)
                return null;

            PacketBase p=null;
            lock(_packets)
            { 
                if (!_packets.TryDequeue(out p))
                   p=null;
            }

            return p;
        }


        
        
    }



    






}
