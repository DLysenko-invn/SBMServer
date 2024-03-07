using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BLE2TCP.BLEEMU
{


    enum SIFOperation
    { 
        NOP,
        Enable,
        Disable,
        Config,

    }

    static class BLEProtocolEncode
    {


        public static byte[] SIFRAWEvent(UInt32 ts, UInt32 ax, UInt32 ay, UInt32 az)
        {
            List<byte> data = new List<byte>();
            data.AddRange(BitConverter.GetBytes(ts));
            data.AddRange(BitConverter.GetBytes(ax));
            data.AddRange(BitConverter.GetBytes(ay));
            data.AddRange(BitConverter.GetBytes(az));
            return data.ToArray();
        }


        public static byte[] SIFRead(bool isenabled)
        {
            byte[] data;
            data = new byte[Const.SIF_PAYLOADSIZE + Const.SIF_HEADERSIZE];
            data[0] = (isenabled) ? Const.ENABLE : Const.DISABLE;
            data[1] = 0;
            data[2] = Const.ENABLE;
            data[3] = 0;
            data[4] = 0;
            return data;
        }



        public static byte[] SIFEvent(UInt32 ts, byte label)
        {
            List<byte> data = new List<byte>();
            data.AddRange(BitConverter.GetBytes(ts));
            data.AddRange(new byte[]{ label });
            return data.ToArray();
        }



        public static byte[] BattStatus()
        {
            return new byte[] { 100 };
        }


        public static byte[]  IMUEvent(UInt32 ts,Int16  x,Int16 y,Int16 z)
        {
            List<byte> data = new List<byte>();
            data.AddRange(BitConverter.GetBytes(ts));
            data.AddRange(BitConverter.GetBytes(x));
            data.AddRange(BitConverter.GetBytes(y));
            data.AddRange(BitConverter.GetBytes(z));
            Int16 gyroxyz=0;
            data.AddRange(BitConverter.GetBytes(gyroxyz));
            data.AddRange(BitConverter.GetBytes(gyroxyz));
            data.AddRange(BitConverter.GetBytes(gyroxyz));
            data.Add(Const.IMU_ACCELVALID);
            return data.ToArray();
        }

        public static byte[] IMURead(bool isenabled, UInt16 acc_odr, byte acc_fsr_enum)
        {
            UInt16 gyr_odr = 0;
            byte gyr_fsr_enum = 0;
            List<byte> data = new List<byte>();
            data.AddRange(BitConverter.GetBytes(acc_odr));
            data.AddRange(BitConverter.GetBytes(gyr_odr));
            data.Add(acc_fsr_enum);
            data.Add(gyr_fsr_enum);
            return data.ToArray();
        }
    }


    static class BLEProtocolDecode
    {

        public static bool IMUData(byte[] data, out int accodr, out int accfsrenum)
        {
            BinaryDataReader reader = new BinaryDataReader(data);
            accodr = reader.GetInt16();
            reader.GetInt16();
            accfsrenum = reader.GetByte();
            return data.Length >= 5;
        }

    }







    static class USBProtocolEncode
    {


        static readonly byte[] NOPAYLOAD = new byte[] { };




        public static Packet SIFDataRead()
        {
            return new Packet(CmdId.CMD_SIF_PARAM_READ, NOPAYLOAD);
        }

        public static Packet SIFRAWDataWrite(bool isenable)
        {
            List<byte> data = new List<byte>();
            data.Add(isenable ? Const.ENABLE : Const.DISABLE);
            return new Packet(CmdId.CMD_SIF_RAW_PARAM_WRITE , data.ToArray());
        }

        public static Packet SIFRAWDataRead()
        {
            return new Packet(CmdId.CMD_SIF_RAW_PARAM_READ, NOPAYLOAD);
        }

        public static Packet IMUDataRead()
        {
            return new Packet(CmdId.CMD_IMU_PARAM_READ, NOPAYLOAD);
        }


        public static Packet SIFDataWrite(SIFOperation cmd, byte[] datachunk)
        {
            byte[] data=null;

            if ( (cmd == SIFOperation.Enable)  || (cmd == SIFOperation.Disable) )
            {
                data = new byte[Const.SIF_PAYLOADSIZE + Const.SIF_HEADERSIZE];
                data[0] = (cmd == SIFOperation.Enable) ? Const.ENABLE : Const.DISABLE;
                data[1] = Const.ENABLE;
                data[2] = 0;
                data[3] = 0;
                data[4] = 0;
                Array.Clear(data, Const.SIF_HEADERSIZE, Const.SIF_PAYLOADSIZE);
            } else
            if ( (cmd == SIFOperation.Config)  )
            { 
                data = new byte[Const.SIF_PAYLOADSIZE + Const.SIF_HEADERSIZE];
                Debug.Assert(datachunk.Length<=Const.SIF_PAYLOADSIZE + Const.SIF_HEADERSIZE - 1); 
                Array.Copy(datachunk,0,data,1,datachunk.Length);
                data[0] = Const.DISABLE;
                data[1] = 0;
                data[2] = 0;
            } else
            { 
                Debug.Assert(false);    
            }

 
            return new Packet(CmdId.CMD_SIF_PARAM_WRITE, data);
        }

        public static Packet IMUDataWrite(bool isenable, UInt16 accodr, byte accfsrenum)
        {
            UInt16 gyrodr = 0;
            byte gyrfsrenum = 0;
            List<byte> data = new List<byte>();
            data.Add(isenable ? Const.ENABLE : Const.DISABLE);
            data.AddRange(BitConverter.GetBytes(accodr));
            data.AddRange(BitConverter.GetBytes(gyrodr));
            data.Add(accfsrenum);
            data.Add(gyrfsrenum);
            return new Packet(CmdId.CMD_IMU_PARAM_WRITE, data.ToArray());
        }

    }

    static class USBProtocolDecode
    {

        class PDR : BinaryDataReader
        {
            public PDR(Packet p) : base(p.Data)
            {
            }
        }


        public static bool SIFDataRead(Packet p, out bool isenable)
        {
            Debug.Assert(p.IsReady);
            PDR reader = new PDR(p);
            isenable = reader.GetByte() != Const.DISABLE;
            return true;
        }

        public static bool SIFEvent(Packet p, out ulong ts, out byte label)
        {
            if (p.Cmd != CmdId.EVENT_SIF_DATA)
            {
                ts = 0; 
                label=0;
                return false;
            }
            Debug.Assert(p.IsReady);
            PDR reader = new PDR(p);
            ts = reader.GetUInt64();
            label = reader.GetByte();
            return true;
        }

        public static bool SIFRAWEvent(Packet p, out UInt64 ts, out UInt32 ax, out UInt32 ay, out UInt32 az)
        {
            Debug.Assert(p.IsReady);
            PDR reader = new PDR(p);
            ts = reader.GetUInt64();
            ax = reader.GetUInt32();
            ay = reader.GetUInt32();
            az = reader.GetUInt32();
            return true;
        }

        public static bool SIFRawDataRead(Packet p, out bool isenable)
        {
            Debug.Assert(p.IsReady);
            PDR reader = new PDR(p);
            isenable = reader.GetByte() != Const.DISABLE;
            return true;
        }


        public static bool IMUDataRead(Packet p, out bool isenable,out int acc_odr,out int acc_fsr_enum )
        {
            
            Debug.Assert(p.IsReady);
            PDR reader = new PDR(p);
            isenable = reader.GetByte() != Const.DISABLE;
            acc_odr = reader.GetUInt16();
            reader.GetUInt16();
            acc_fsr_enum = reader.GetByte();
            return true;
        }


        //todo: implement gyro support
        public static bool IMUEvent(Packet p, out UInt64 ts, out Int16 ax,out Int16 ay,out Int16 az)
        {
            Debug.Assert(p.IsReady);
            PDR reader = new PDR(p);
            ts = reader.GetUInt64();
            ax = reader.GetInt16();
            ay = reader.GetInt16();
            az = reader.GetInt16();
            return true;
        }





    }








}
