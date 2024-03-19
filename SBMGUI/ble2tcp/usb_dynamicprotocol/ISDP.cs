using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DynamicProtocol
{


    public class ISDPException : Exception
    { 

        public ISDPException(string message): base(message)
        {
        }


    }




    public class ISDPFrame
    {
        enum Stage
        { 
            SYNC0 = 0,
            SYNC1 = 1,
            SIZE =  2,
            PAYLOAD = 10,
            DONE = 999,
        }

        byte[] EMPTY = new byte[]{};


        Stage _stage;    
        byte[] _data;
        bool _protocolerrordetected;
        int _nextreadsize;


        public ISDPFrame()
        { 
            _stage = Stage.SYNC0;
            _data = null;
            _protocolerrordetected = false;
            _nextreadsize = 1;

        }

        public int MaxRead
        {   get{   return _nextreadsize;}
        }



        public bool IsError
        {   get{   return _protocolerrordetected;}
        }





        /// <summary>
        /// Buld a DP frame
        /// </summary>
        /// <param name="d">Bytes</param>
        /// <returns>Number of bytes consumed</returns>
        public int Put(byte[] d)
        { 
            if ((d==null) || (d.Length==0))
                return 0;

            switch(_stage)
            {   
                case Stage.SYNC0:
                    if (d[0]==ISDP.MAGIC0)
                    {   _stage=Stage.SYNC1;
                        _nextreadsize = 1;
                        return 1;
                    }
                    break;

                case Stage.SYNC1:
                    if (d[0]==ISDP.MAGIC1)
                    {   _stage=Stage.SIZE;
                        _nextreadsize = 2;
                        return 1;
                    }
                    _stage=Stage.SYNC0;
                    _nextreadsize = 1;
                    _protocolerrordetected = true;
                    return 0;
                
                case Stage.SIZE:
                    Debug.Assert(d.Length == 2);
                    _stage=Stage.PAYLOAD;
                    _nextreadsize = d[0] + d[1]*0x100;
                    return 2;

                case Stage.PAYLOAD:
                    int n = d.Length;
                    Debug.Assert( n<=_nextreadsize );
                    _nextreadsize -= n;
                    if (_data==null)
                    {   _data = ISDP.ConcatArray(d,null);
                    } else
                    {   _data = ISDP.ConcatArray(_data,d);
                    }
                    if (_nextreadsize==0)
                        _stage=Stage.DONE;
                     return n;
                case Stage.DONE:
                    throw new ISDPException("Writing after frame end");
        
            }
            throw new ISDPException("Wrong FSA stage");
        }

    
        public bool IsFinished
        {   get{    return _stage == Stage.DONE;} 
        }


        public int? GetByteAt(int index)
        { 
            if ( (IsFinished) || (_data == null) || (index>=_data.Length) || (index<0) )
                return null;
            return _data[index];
        }


        public byte[] GetBytesAfter(int index)
        { 
            if ( (IsFinished) || (_data == null) || (index>=_data.Length) || (index<0) )
                return null;

            int n = _data.Length - index;
            byte[] tail = new byte[ n ];
            Array.Copy(_data, index, tail, 0, n);
            return tail;
        }



        public byte[] ToBytes()
        { 
            byte[] payload = _data==null ? EMPTY : _data;
            int size = payload.Length;
            byte[] data = new byte[4+size];
            data[0] = ISDP.MAGIC0;
            data[1] = ISDP.MAGIC1;
            data[2] = (byte)( (size) & 0xFF );
            data[3] = (byte)( (size >> 8) & 0xFF ); 
            Array.Copy(payload, 0,data, 4, size);

            return data;
        }



        public  static ISDPFrame FromPayload(byte[] payload)
        { 
            ISDPFrame frame = new ISDPFrame();
            frame._stage = Stage.DONE;
            frame._data = payload;
            return frame;
        }



    }






    public class ISDPCmd
    { 
        const int GROUPID = 3;

        static string[] TYPESTR = new string[]{"C","R","'A"};

        const string STR_EMPTY = "EMPTY";
        const string STR_NULL = "NULL";

        ISDPFrame _frame;

        public ISDPCmd(ISDPFrame frame)
        {
            Debug.Assert(frame!=null);
            _frame = frame;
        }

        public string ETYPEStr
        {   get{   return (ETYPE<TYPESTR.Length) ? TYPESTR[ETYPE] : string.Format("{0:X2}", ETYPE );  }
        }

        public int ETYPE
        {   get{    return (GID & 0xC0) >> 6;}
        }

        public int GID
        {   get{    return _frame.GetByteAt(0) ?? ISDP.ERROR;}
        }
    
        static public int MakeGID(int etype)
        {   return ((etype << 6) | (ISDPCmd.GROUPID));
        }
    
        public ISDPFrame Frame
        {   get{    return _frame; }
        }

        public int GroupID
        {   get{     return GID & 0x3F;}  
        }

        public int EID
        {   get{     return _frame.GetByteAt(1) ?? ISDP.ERROR;} 
        }

        public byte[] Payload
        {   get{    return _frame.GetBytesAfter(2); }   
        }

        static public ISDPCmd FromFrame(ISDPFrame frame)
        { 
            ISDPCmd cmd = new ISDPCmd(frame);
            if ((cmd.ETYPE != ISDP.ETYPE_RESP) && (cmd.ETYPE != ISDP.ETYPE_ASYNC))
                throw new ISDPException("Wrong frame ETYPE in "+ frame.ToString());
            return cmd;
        }

        public override string ToString()
        {
            if (_frame==null)
                return STR_NULL;
    
            byte[] data = _frame.ToBytes();
            if (data.Length==0)
                return STR_EMPTY;

            return ETYPEStr +' ' + string.Format("{0:X2}",EID)  +  ToHEXString(Payload);
        }

        public string ToHEXString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:X2}", b);
            return hex.ToString();
        }


    }







    public static class ISDP
    {

        #region CONSTANTS

        public const int MAGIC0 = 0x55;
        public const int MAGIC1 = 0xAA;

        public const int ERROR = -1;

        public const int ETYPE_CMD   = 0;
        public const int ETYPE_RESP  = 1;
        public const int ETYPE_ASYNC = 2;


        public const int SID_NULL                         = 0  ;
        public const int SID_RESERVED                     = 0  ;
        public const int SID_ACCELEROMETER                = 1  ;
        public const int SID_MAGNETOMETER                 = 2  ;
        public const int SID_ORIENTATION                  = 3  ;
        public const int SID_GYROSCOPE                    = 4  ;
        public const int SID_LIGHT                        = 5  ;
        public const int SID_PRESSURE                     = 6  ;
        public const int SID_TEMPERATURE                  = 7  ;
        public const int SID_PROXIMITY                    = 8  ;
        public const int SID_GRAVITY                      = 9  ;
        public const int SID_LINEAR_ACCELERATION          = 10 ;
        public const int SID_ROTATION_VECTOR              = 11 ;
        public const int SID_HUMIDITY                     = 12 ;
        public const int SID_AMBIENT_TEMPERATURE          = 13 ;
        public const int SID_UNCAL_MAGNETOMETER           = 14 ;
        public const int SID_GAME_ROTATION_VECTOR         = 15 ;
        public const int SID_UNCAL_GYROSCOPE              = 16 ;
        public const int SID_SMD                          = 17 ;
        public const int SID_STEP_DETECTOR                = 18 ;
        public const int SID_STEP_COUNTER                 = 19 ;
        public const int SID_GEOMAG_ROTATION_VECTOR       = 20 ;
        public const int SID_HEART_RATE                   = 21 ;
        public const int SID_TILT_DETECTOR                = 22 ;
        public const int SID_WAKE_GESTURE                 = 23 ;
        public const int SID_GLANCE_GESTURE               = 24 ;
        public const int SID_PICK_UP_GESTURE              = 25 ;
        public const int SID_BAC                          = 26 ;
        public const int SID_PDR                          = 27 ;
        public const int SID_B2S                          = 28 ;
        public const int SID_3AXIS                        = 29 ;
        public const int SID_EIS                          = 30 ;
        public const int SID_OIS                          = 31 ;
        public const int SID_RAW_ACCELEROMETER            = 32 ;
        public const int SID_RAW_GYROSCOPE                = 33 ;
        public const int SID_RAW_MAGNETOMETER             = 34 ;
        public const int SID_RAW_TEMPERATURE              = 35 ;
        public const int SID_CUSTOM_PRESSURE              = 36 ;
        public const int SID_MIC                          = 37 ;
        public const int SID_TSIMU                        = 38 ;
        public const int SID_RAW_PPG                      = 39 ;
        public const int SID_HRV                          = 40 ;
        public const int SID_SLEEP_ANALYSIS               = 41 ;
        public const int SID_BAC_EXTENDED                 = 42 ;
        public const int SID_BAC_STATISTICS               = 43 ;
        public const int SID_FLOOR_CLIMB_COUNTER          = 44 ;
        public const int SID_ENERGY_EXPENDITURE           = 45 ;
        public const int SID_DISTANCE                     = 46 ;
        public const int SID_SHAKE                        = 47 ;
        public const int SID_DOUBLE_TAP                   = 48 ;
        public const int SID_TYPE_CUSTOM3                 = 52 ;// <--
        public const int SID_TYPE_CUSTOM4                 = 53 ;// <--

        public const int EID_WHO_AM_I                     = 0x10;
        public const int EID_RESET                        = 0x11;
        public const int EID_SETUP                        = 0x12;
        public const int EID_CLEANUP                      = 0x13;
        public const int EID_SELF_TEST                    = 0x15;
        public const int EID_GET_FW_INFO                  = 0x16;
        public const int EID_PING_SENSOR                  = 0x17;
        public const int EID_START_SENSOR                 = 0x19;
        public const int EID_STOP_SENSOR                  = 0x1A;
        public const int EID_SET_SENSOR_PERIOD            = 0x1B;
        public const int EID_SET_SENSOR_TIMEOUT           = 0x1C;
        public const int EID_FLUSH_SENSOR                 = 0x1D;
        public const int EID_SET_SENSOR_BIAS              = 0x1E;
        public const int EID_GET_SENSOR_BIAS              = 0x1F;
        public const int EID_SET_SENSOR_MMATRIX           = 0x20;
        public const int EID_GET_SENSOR_DATA              = 0x21;
        public const int EID_GET_SW_REG                   = 0x22;
        public const int EID_SET_SENSOR_CFG               = 0x23;// <--
        public const int EID_GET_SENSOR_CFG               = 0x24;
        public const int EID_NEW_SENSOR_DATA              = 0x30;

        public const int CONFIG_RESERVED        = 0  ; // reserved - do not use 
        public const int CONFIG_REFERENCE_FRAME = 1  ; // sensor reference frame (aka 'mouting matrix') 
        public const int CONFIG_GAIN            = 2  ; // sensor gain to be applied on sensor data 
        public const int CONFIG_OFFSET          = 3  ; // sensor offset to be applied on sensor data 
        public const int CONFIG_CONTEXT         = 4  ; // arbitray context buffer 
        public const int CONFIG_FSR             = 5  ; // sensor's full scale range 
        public const int CONFIG_BW              = 6  ; // sensor's bandwidth 
        public const int CONFIG_AVG             = 7  ; // sensor's average 
        public const int CONFIG_RESET           = 8  ; // Reset the specified service 
        public const int CONFIG_POWER_MODE      = 9  ; // Change the power mode of the sensor 
        public const int CONFIG_CUSTOM          = 32 ; // base value to indicate custom config 
        public const int CONFIG_MAX             = 64 ; // absolute maximum value for config type 

        public const int INV_SENSOR_CONFIG_RESERVED                  = 0  ;        
        public const int INV_SENSOR_CONFIG_MOUNTING_MATRIX           = 1  ;
        public const int INV_SENSOR_CONFIG_GAIN                      = 2  ;
        public const int INV_SENSOR_CONFIG_OFFSET                    = 3  ;
        public const int INV_SENSOR_CONFIG_CONTEXT                   = 4  ;
        public const int INV_SENSOR_CONFIG_FSR                       = 5  ;
        public const int INV_SENSOR_CONFIG_BW                        = 6  ;
        public const int INV_SENSOR_CONFIG_AVG                       = 7  ;
        public const int INV_SENSOR_CONFIG_RESET                     = 8  ;
        public const int INV_SENSOR_CONFIG_POWER_MODE                = 9  ;
        public const int INV_SENSOR_CONFIG_MIN_PERIOD                = 10 ;
        public const int INV_SENSOR_CONFIG_CUSTOM                    = 128;     
        public const int INV_SENSOR_CONFIG_PRED_GRV                  = 131;     
        public const int INV_SENSOR_CONFIG_BT_STATE                  = 132;     
        public const int INV_SENSOR_CONFIG_REPORT_EVENT              = 133; 
        public const int INV_SENSOR_CONFIG_FNM_OFFSET                = 134;   
        public const int INV_SENSOR_CONFIG_ALGO_SETTINGS             = 138;// <--
        public const int INV_SENSOR_CONFIG_MAX                       = 255;     

        public const int INV_ERROR_SUCCESS      = 0   ; // no error 
        public const int INV_ERROR              = 0xFF; // unspecified error 
        public const int INV_ERROR_NIMPL        = 0xFE; // function not implemented for given arguments 
        public const int INV_ERROR_TRANSPORT    = 0xFD; // error occured at transport level 
        public const int INV_ERROR_TIMEOUT      = 0xFC; // action did not complete in the expected `time window 
        public const int INV_ERROR_SIZE         = 0xFB; // size/length of given arguments is not suitable to complete requested action 
        public const int INV_ERROR_OS           = 0xFA; // error related to OS 
        public const int INV_ERROR_IO           = 0xF9; // error related to IO operation 
        public const int INV_ERROR_MEM          = 0xF7; // not enough memory to complete requested action 
        public const int INV_ERROR_HW           = 0xF6; // error at HW level 
        public const int INV_ERROR_BAD_ARG      = 0xF5; // provided arguments are not good to perform requestion action 
        public const int INV_ERROR_UNEXPECTED   = 0xF4; // something unexpected happened 
        public const int INV_ERROR_FILE         = 0xF3; // cannot access file or unexpected format 
        public const int INV_ERROR_PATH         = 0xF2; // invalid file path 
        public const int INV_ERROR_IMAGE_TYPE   = 0xF1; // error when image type is not managed 
        public const int INV_ERROR_WATCHDOG     = 0xF0; // error when device doesn't respond  to ping 


        #endregion


        public static int GIDCMD = ISDPCmd.MakeGID(ISDP.ETYPE_CMD);

        public const int CFG_PAYLOAD_SIZE = 64;

        static ISDP()
        {
            Debug.Assert( BitConverter.IsLittleEndian );
        }


        public static byte[] ToByteArray(int[] payload)
        { 
            byte[] p = null;
            if (payload!=null)
            {
                int n = payload.Length;
                p = new byte[n];
                for(int i=0;i<n;i++)
                    p[i] = (byte)payload[i];
            }
            return p;
        }

        public static byte[] ConcatArray(byte[] a,byte[] b)
        {
            Debug.Assert(a!=null);
            byte[] c = new byte[ a.Length + ( (b==null) ? 0 : b.Length) ];
            a.CopyTo(c, 0);
            if (b!=null)            
                b.CopyTo(c, b.Length );
            return c;
        }

/*

        byte[] SubArray(byte[] a,int index,int count)
        {
            Debug.Assert(a!=null);
            Debug.Assert(a.Length<=index+count);
            byte[] r = new byte[count];
            Array.Copy(a,index,r,0,count);
            return r;
        }
*/


        static byte[] Int16ToBytes(int n)
        {
            return BitConverter.GetBytes( (UInt16)n );
        }
    
        public static ISDPCmd WhoAmI()
        {   return  new ISDPCmd( ISDPFrame.FromPayload( ToByteArray(new int[]{ GIDCMD , EID_WHO_AM_I }) ) );  
        }

        // 55aa 0300 03  19  20               -- en 32
        public static ISDPCmd Start(int sensorid)
        {   return  new ISDPCmd( ISDPFrame.FromPayload( ToByteArray(new int[]{ GIDCMD , EID_START_SENSOR, sensorid }) ) );
        }

        public static ISDPCmd Stop(int sensorid)
        {   return  new ISDPCmd( ISDPFrame.FromPayload( ToByteArray(new int[]{ GIDCMD , EID_STOP_SENSOR, sensorid }) ) );
        }


        public static ISDPCmd SetConfig(int sensorid, int cfgtype, byte[] cfgdata)
        { 
            int cfgsize = cfgdata.Length;
            Debug.Assert( cfgsize <= CFG_PAYLOAD_SIZE);
            byte[] data = ToByteArray( new int[]{ ISDP.GIDCMD ,ISDP.EID_SET_SENSOR_CFG , sensorid, cfgtype ,cfgsize  } );
            data = ConcatArray(data,cfgdata);
            if (cfgsize<CFG_PAYLOAD_SIZE)
                data = ConcatArray( data, new byte[CFG_PAYLOAD_SIZE - cfgsize]);
            return  new ISDPCmd( ISDPFrame.FromPayload( data ) );
        }



        // 55AA 4500 03 23 20   05 04 02000000...      - setconfig 32 fsr=2
        public static ISDPCmd SetConfigFSR(int sensorid,int fsrvalue)
        {   return ISDP.SetConfig(sensorid, CONFIG_FSR,  Int16ToBytes( fsrvalue ) );
        }


        // 55aa 0700 03  1B  01 20 A1 07 00   -- odr acc 500
        public static ISDPCmd SetConfigODR(int sensorid,int odr_hz)
        { 
            const int tomicriseconds = 1000000;
            int period_ms = (odr_hz!=0) ? (int)( (1.0/odr_hz)*tomicriseconds ) : 0;
            byte[] period_data = Int16ToBytes( period_ms) ;
            byte[] data = ToByteArray(  new int[]{ ISDP.GIDCMD ,ISDP.EID_SET_SENSOR_PERIOD , sensorid } );
            data  = ConcatArray(data, period_data); 
            return  new ISDPCmd( ISDPFrame.FromPayload( data ) );
        }



        // odr racc 1/100
        // 55aa 0700 03  1b 20 10 27 00 00
        // 55aa 0300 43  1b 00



        #region DECODERS


        public class Data
        {
            public int sensorst = 0;
            public int sensorid = 0;
            public long timestamp = 0;

            public int accuracy=0;

            public double x = 0;
            public double y = 0;
            public double z = 0;


        }
    


        public abstract class CmdDecoder
        { 

            protected int Bytes_to_uint16(byte[] data, int pos)
            { 
                Debug.Assert(data.Length<=pos+2);
                return BitConverter.ToUInt16(data, pos);
            }

            protected long Bytes_to_uint24(byte[] data, int pos)
            { 
                Debug.Assert(data.Length<=pos+3);
                byte[] r = new byte[4]{ 0,data[pos],data[pos],data[pos] };
                return Bytes_to_uint32(r,0);
            }

            protected int Bytes_to_int16(byte[] data, int pos)
            { 
                Debug.Assert(data.Length<=pos+2);
                return BitConverter.ToInt16(data, pos);
            }

            protected int Bytes_to_int32(byte[] data, int pos)
            { 
                Debug.Assert(data.Length<=pos+4);
                return BitConverter.ToInt32(data, pos);
            }

            protected long Bytes_to_uint32(byte[] data, int pos)
            { 
                Debug.Assert(data.Length<=pos+4);
                return BitConverter.ToUInt32(data, pos);
            }

            protected long Bytes_to_int24(byte[] data, int pos)
            { 
                Debug.Assert(data.Length<=pos+3);
                byte[] r = new byte[4]{ 0,data[pos],data[pos],data[pos] };
                return Bytes_to_int32(r,0);
            }

            protected double Bytes_to_int16_Q11(byte[] data, int pos)
            { 
                return  ((double)Bytes_to_int16(data,pos)) /  (1 << 11) ;
            }

            protected double Bytes_to_int16_Q4(byte[] data, int pos)
            {   return  ((double)Bytes_to_int16(data,pos)) /  (1 << 4) ;
            }


            public abstract  Data Decode(ISDPCmd c);

        }



        public class CmdSensorData:CmdDecoder
        { 

            public override Data Decode(ISDPCmd c)
            {
                if (c.EID!=ISDP.EID_NEW_SENSOR_DATA)
                    return null;

                Data r = new Data();
                byte[] d = c.Payload;
                if (d.Length<6)
                    throw new ISDPException( "Wrong payload length in "+ c.ToString());
                r.sensorst = d[0] | 0x03;
                r.sensorid = d[1];
                r.timestamp = Bytes_to_uint32(d,2);
                return r;
            }
        }

        public class CmdSensorDataAccel:CmdSensorData
        { 
            public override Data Decode(ISDPCmd c)
            {
                Data r = base.Decode(c);
                if (r==null)
                    return null;
                if (r.sensorid!=ISDP.SID_ACCELEROMETER)
                    return null;
                byte[] d = c.Payload;
                if (d.Length!=13)
                    throw new ISDPException("Wrong sensor data length in "+ c.ToString());
                r.accuracy = d[12];
                r.x = Bytes_to_int16_Q11(d,6) ;
                r.y = Bytes_to_int16_Q11(d,8) ;
                r.z = Bytes_to_int16_Q11(d,10) ;
                return r;
            }
        }


        public class CmdSensorDataGyro:CmdSensorData
        { 
            public override Data Decode(ISDPCmd c)
            { 
                Data r = base.Decode(c);
                if (r==null)
                    return null;
                if (r.sensorid!=ISDP.SID_GYROSCOPE)
                    return null;

                byte[] d = c.Payload;
                if (d.Length!=13)
                    throw new ISDPException("Wrong sensor data length in "+ c.ToString());

                r.accuracy = d[12];
                r.x = Bytes_to_int16_Q4(d,6) ;
                r.y = Bytes_to_int16_Q4(d,8) ;
                r.z = Bytes_to_int16_Q4(d,10) ;
                return r;
            }
        }




        public class CmdSensorDataRawAccGyro:CmdSensorData
        { 
            // 55aa 0e00 83 30 00 20  3ec35b42  5400 80ff 5820  - EVENT D SENSOR_RAW_ACCELEROMETER id: 0x00000020 t: 1113310014 us: 0 data: 84 -128 8280

            public override Data Decode(ISDPCmd c)
            { 
                Data r = base.Decode(c);
                if (r==null)
                    return null;
                if ( (r.sensorid!=ISDP.SID_RAW_ACCELEROMETER) && (r.sensorid!=ISDP.SID_RAW_GYROSCOPE) )
                    return null;
                byte[] d = c.Payload;
                if (d.Length==12)
                {
                    r.x = Bytes_to_int16(d,6);
                    r.y = Bytes_to_int16(d,8);
                    r.z = Bytes_to_int16(d,10);
                    return r;
                }
                if (d.Length==15)
                { 
                    r.x = Bytes_to_int24(d,6);
                    r.y = Bytes_to_int24(d,9);
                    r.z = Bytes_to_int24(d,12);
                    return r;
                }

                throw new ISDPException("Wrong sensor data length in "+ c.ToString());
            }


        }


        #endregion



        static CmdDecoder[] DECODERS = new CmdDecoder[]{ new CmdSensorDataAccel(),new CmdSensorDataGyro(),new CmdSensorDataRawAccGyro()};

        public static Data Decode(ISDPCmd c , CmdDecoder[] customdecoders = null)
        { 
            CmdDecoder[] decoders = (customdecoders == null)  ? DECODERS : customdecoders ;
            foreach(CmdDecoder d in decoders)
            {   Data r = d.Decode(c);
                if (r!=null)
                    return r;
            }
            throw new ISDPException("No decoder found for "+ c.ToString());
        }



    }








 
}
