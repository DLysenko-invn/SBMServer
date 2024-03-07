using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BLE2TCP.BLEEMU
{

    interface ITalkMaker
    { 
        Talk MakeATalk(Packet packettosend);
    }

    interface IPacketProcessor
    {
        void ProcessPacket(Packet p);

    }






    class BinaryDataReader
    {
        const byte DEFAULT_BYTE = 0;
        const UInt16 DEFAULT_UInt16 = 0;
        const Int16 DEFAULT_Int16 = 0;
        const UInt32 DEFAULT_UInt32 = 0;
        const Int32 DEFAULT_Int32 = 0;
        const UInt64 DEFAULT_UInt64 = 0;

        protected byte[] _data;
        protected int _pos;
        public BinaryDataReader(byte[] data)
        {
            _data = data;
            _pos = 0;
        }

        public int Pos
        {
            get { return _pos; }
        }

        public bool CheckPos(int n)
        {
            return ((_pos + n) <= _data.Length);
        }

        public byte GetByte()
        {
            if (_pos + sizeof(byte) > _data.Length)
                return DEFAULT_BYTE;
            return _data[_pos++];
        }

        public UInt16 GetUInt16()
        {
            if (_pos + sizeof(UInt16) > _data.Length)
                return DEFAULT_UInt16;
            UInt16 v = BitConverter.ToUInt16(_data, _pos);
            _pos += sizeof(UInt16);
            return v;
        }

        public Int16 GetInt16()
        {
            if (_pos + sizeof(Int16) > _data.Length)
            {
                _pos = _data.Length;
                return DEFAULT_Int16;
            }
            Int16 v = BitConverter.ToInt16(_data, _pos);
            _pos += sizeof(Int16);
            return v;
        }

        public UInt32 GetUInt32()
        {
            if (_pos + sizeof(UInt32) > _data.Length)
            {
                _pos = _data.Length;
                return DEFAULT_UInt32;
            }
            UInt32 v = BitConverter.ToUInt32(_data, _pos);
            _pos += sizeof(UInt32);
            return v;
        }

        public Int32 GetInt32()
        {
            if (_pos + sizeof(Int32) > _data.Length)
            {
                _pos = _data.Length;
                return DEFAULT_Int32;
            }
            Int32 v = BitConverter.ToInt32(_data, _pos);
            _pos += sizeof(Int32);
            return v;
        }

        public UInt64 GetUInt64()
        {
            if (_pos + sizeof(UInt64) > _data.Length)
            {
                _pos = _data.Length;
                return DEFAULT_UInt64;
            }
            UInt64 v = BitConverter.ToUInt64(_data, _pos);
            _pos += sizeof(UInt64);
            return v;
        }

        public string GetString()
        {
            string s = GetStr(_data, _pos, out int newpos);
            _pos = newpos;
            return s;
        }

        static string GetStr(byte[] data, int pos, out int newpos)
        {
            if (pos >= data.Length)
            {
                newpos = pos;
                return string.Empty;
            }
            int i = pos;
            StringBuilder s = new StringBuilder();
            while ((i < data.Length) && (data[i] != 0))
            {
                s.Append(Convert.ToChar(data[i]));
                i++;
            }
            newpos = i + 1;
            return s.ToString();
        }



        public double? GetQ24()
        {
            return GetInt32Q(24);
        }

        public double? GetQ16()
        {
            return GetInt32Q(16);
        }

        public double? GetQ27()
        {
            return GetInt32Q(27);
        }


        public double? GetInt32Q(int N)
        {
            if (_pos + sizeof(Int32) > _data.Length)
            {
                _pos = _data.Length;
                return null;
            }
            Int32 v = BitConverter.ToInt32(_data, _pos);
            _pos += sizeof(Int32);
            double f = QtoDouble(v, N);
            return f;
        }

        double QtoDouble(Int32 n, int N)
        {
            return (double)n / (1 << N);
        }

        public static double RADtoDEG(double rad)
        {
            return ((rad * 180.0) / Math.PI) % 360.0;
        }



    }












}
