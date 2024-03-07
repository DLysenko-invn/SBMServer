using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace BLE2TCP.BLEEMU
{



    enum CmdType
    {
        Unknown,
        Read,
        Write,
        Indication,
    }

    enum CmdId
    {
        UNKNOWN = 0,

        CMD_SIF_PARAM_WRITE	     = 0x95,
        CMD_SIF_PARAM_READ       = 0x55,
        EVENT_SIF_DATA		     = 0xD5,				
						
        CMD_SIF_RAW_PARAM_WRITE	 = 0x96,
        CMD_SIF_RAW_PARAM_READ	 = 0x56,
        EVENT_SIF_RAW_DATA		 = 0xD6,				

        CMD_IMU_PARAM_WRITE      = 0x81,
        CMD_IMU_PARAM_READ       = 0x41,
        EVENT_IMU_DATA           = 0xC1,





        ERROR = 0xFF,


    }

    class CmdInfo
    {

        public CmdInfo(CmdId id, CmdType t, bool issend, int sendsize, bool isrecv, int recvsize_ok, int recvsize_error, IFakeCharacteristic characteristic)
        {
            this.type = t;
            this.id = id;
            this.issend = issend;
            this.sendsize = sendsize;
            this.isrecv = isrecv;
            this.recvsize_ok = recvsize_ok;
            this.recvsize_error = recvsize_error;
            this.characteristic = characteristic;
        }


        public CmdType type;
        public CmdId id;
        public bool issend;
        public int sendsize;
        public bool isrecv;
        public int recvsize_ok;
        public int recvsize_error;
        public IFakeCharacteristic characteristic;



    }




    #region TALK

    enum TalkStage
    {
        Unknown,
        Waiting,
        DoneOk,
        Ready,
        DoneFail,
        InQueue,

    }

    class Talk
    {
        public static Talk NOTALK = new Talk() { Stage = TalkStage.DoneOk };
        public Talk()
        {
            _doneevent = new ManualResetEvent(false);
            _stage = TalkStage.Unknown;

        }

        public Talk(Packet send, CmdInfo info):this()
        {
            Debug.Assert(info != null);
            sendinfo = info;
            senddata = send;
            recvdata = new Packet();
            Stage = TalkStage.Ready;
        }

        ManualResetEvent _doneevent;

        public Packet senddata;
        public Packet recvdata;
        public CmdInfo sendinfo;
        public CmdInfo recvinfo;
        public DateTime starttime;
        public int timeout_ms;


        TalkStage _stage;
        public TalkStage Stage
        {
            get { return _stage;}
            set {
                    _stage = value;
                    if (IsDone)
                        _doneevent.Set();
                }
            
        }

        public bool WaitTillDone(int timeout)
        { 
            return _doneevent.WaitOne(timeout);
        }


        public bool IsDone
        {   get
            {   return (Stage == TalkStage.DoneFail) || (Stage == TalkStage.DoneOk);
            }
        }

        public bool IsDoneOK
        {   get
            {   return (Stage == TalkStage.DoneOk);
            }
        }



    }

    #endregion







    #region PACKET
    class Packet
    {

        static readonly byte[] MAGIC = new byte[] { 0x55, 0xAA };

        int _nextportionsize;
        int _pos;
        CmdId _id;
        int _size;
        byte[] _data;
        int _datapos;
        bool _isready;


        public Packet()
        {
            Clear();
        }

        public Packet(CmdId id, byte[] payload) : this()
        {
            _id = id;
            _data = payload;
            _isready = true;
        }



        byte SizeByte
        {
            get
            {
                int size = (_data == null) ? 0 : _data.Length;
                Debug.Assert(size <= byte.MaxValue);
                if (size > byte.MaxValue)
                    return 0;
                return (byte)(size + 1);
            }
            set
            {
                _size = value - 1;
            }
        }

        public byte[] ToBytes()
        {
            if (!_isready)
                return null;
            List<byte> d = new List<byte>();
            d.AddRange(MAGIC);
            d.Add(SizeByte);
            d.Add((byte)_id);
            if (_data != null)
                d.AddRange(_data);
            return d.ToArray();
        }


        const char DELIM = ' ';
        const string FORMAT = "{0:X02}";
        const string TOBECONTINUED = "...";
        const int SHORTMAX = 30;

        public string LogString(bool isshort = false)
        {
            StringBuilder s = new StringBuilder();
            s.Append(string.Format(FORMAT, MAGIC[0]));
            s.Append(string.Format(FORMAT, MAGIC[1]));
            s.Append(DELIM);
            s.Append(string.Format(FORMAT, SizeByte));
            s.Append(DELIM);
            s.Append(string.Format(FORMAT, ((int)_id)));
            s.Append(DELIM);

            LogData(s, _data, isshort);

            return s.ToString();
        }


        static void LogData(StringBuilder s, byte[] data, bool isshort = false)
        {

            if (data != null)
                foreach (byte b in data)
                {
                    s.Append(string.Format(FORMAT, b));
                    if ((isshort) && (s.Length >= SHORTMAX))
                    {
                        s.Append(TOBECONTINUED);
                        break;
                    }
                }
        }

        public static string ToHEXLine(byte[] data, bool isshort = false)
        {
            StringBuilder s = new StringBuilder();
            LogData(s, data, isshort);
            return s.ToString();
        }



        public override string ToString()
        {
            return LogString(true);
        }



        public byte[] Data
        {
            get { return _data; }
        }

        public void Clear()
        {
            _nextportionsize = 3;
            _pos = 0;
            _id = CmdId.UNKNOWN;
            _size = 0;
            _data = null;
            _datapos = 0;
            _isready = false;

        }

        public bool IsReady
        {
            get { return _isready; }
        }


        public CmdId Cmd
        {
            get { return _id; }
        }

        public int NextPortion
        {
            get { return _nextportionsize; }
        }

        public bool Add(byte b)
        {
            Debug.Assert(_isready == false);

            switch (_pos)
            {
                case 0:
                    if (b == MAGIC[0])
                    {
                        _pos++;
                        _nextportionsize = 3;
                    }
                    else
                    {
                        Clear();
                    }
                    break;
                case 1:
                    if (b == MAGIC[1])
                    {
                        _pos++;
                        _nextportionsize = 2;
                    }
                    else
                    {
                        Clear();
                    }
                    break;
                case 2:
                    _pos++;
                    SizeByte = b;
                    _nextportionsize = b;
                    break;
                case 3:
                    _pos++;
                    //_id = (CmdId)b;
                    bool isvalid = Enum.IsDefined(typeof(CmdId), ((int)b));
                    Debug.Assert(isvalid);
                    _id = isvalid ? (CmdId)b : CmdId.UNKNOWN;
                    if (_size == 0)
                    {
                        _data = new byte[] { };
                        FinalizePacket();
                    }
                    else
                    {
                        _nextportionsize = _size;
                        _data = new byte[_size];
                        _datapos = 0;
                    }
                    break;
                default:
                    _pos++;
                    _nextportionsize--;
                    _data[_datapos] = b;
                    _datapos++;
                    if (_datapos >= _size)
                        FinalizePacket();
                    break;
            }

            return _isready;
        }

        void FinalizePacket()
        {
            _isready = true;
        }


        public static bool Compare(Packet p1, Packet p2)
        {
            if (p1._id != p2._id)
                return false;
            if (p1._size != p2._size)
                return false;
            if ((p1._data == null) || (p2._data == null))
                return false;

            return p1._data.SequenceEqual(p2._data);
        }

        public bool IsErrorResponse()
        {
            return ((_size == 1) && (_data[0] == Const.ERROR));
        }


    }

    #endregion



    class Const
    {

        public const byte ENABLE = 1;
        public const byte DISABLE = 0;
        public const byte RESERVED = 0;
        public const byte ACCEL_ENABLED = 1;
        public const byte GYRO_ENABLED = 2;
        public const byte DISABLED = 0;
        public const byte ERROR = 0xFF;



        public const int  SIF_PAYLOADSIZE = 60;
        public const int  SIF_HEADERSIZE = 5;

        public const int  IMU_ACCELVALID = 1;



        public enum AccelFSR : byte
        {
            Unknown = 99,
            FSR_4G = 1,
            FSR_8G = 2,
            FSR_16G = 3,
            FSR_32G = 4
        }

        public enum GyroFSR : byte
        {
            Unknown = 99,
            FSR_500dps = 5,
            FSR_1000dps = 6,
            FSR_2000dps = 7,
            FSR_4000dps = 8,
        }

        static public  AccelFSR AccelFSRFromValue(int v)
        {   string enumstr = "FSR_" + v.ToString() + "G";
            if (Enum.TryParse(enumstr, out AccelFSR e))
                return e;
            return AccelFSR.Unknown;
        }

        static public int  AccelFSRToValue(AccelFSR fsr)
        {   string s = fsr.ToString();
            if (s.StartsWith("FSR_"))
                return int.Parse(s.Substring(4,s.Length-1-4));
            return 0;
        }


        static public GyroFSR GyroFSRFromValue(int v)
        {   string enumstr = "FSR_" + v.ToString() + "dps";
            if (Enum.TryParse(enumstr, out GyroFSR e))
                return e;
            return GyroFSR.Unknown;
        }

        static public int GyroFSRToValue(GyroFSR fsr)
        {   string s = fsr.ToString();
            if (s.StartsWith("FSR_"))
                return int.Parse(s.Substring(4,s.Length-3-4));
            return 0;
        }


    }




















}