using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Windows.Web.Syndication;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BLE2TCP
{



    class PacketPayloadBytes : IPayload
    {
        static byte[] EMPTY = new byte[] { };
        int _startindex;
        int _size;
        byte[] _data;

        public PacketPayloadBytes()
        {
            _startindex = 0;
            _size = 0;
            _data = new byte[] { };

        }

        public PacketPayloadBytes(byte[] data)
        {
            FromBytes(data);

        }

        public PacketPayloadBytes(byte[] data, int startindex, int size)
        {
            FromBytes(data, startindex, size);
        }

        public void FromBytes(byte[] data)
        {
            _startindex = 0;
            _size = data.Length;
            _data = data;
        }

        public void FromBytes(byte[] data, int startindex, int size)
        {
            Debug.Assert(startindex+size <= data.Length);
            _startindex = startindex;
            _size = size;
            _data = data;
        }

        public byte[] ToBytes()
        {
            Debug.Assert(_startindex+_size<= _data.Length);
            if (_size == 0)
                return EMPTY;

            byte[] data = new byte[_size];
            Array.Copy(_data, _startindex, data, 0, _size);
            return data;
        }


        public int ByteSize
        {
            get { return _size; }
        }

    }

    class PacketPayloadNothing : IPayload
    {

        static byte[] EMPTY = new byte[] { };

        public PacketPayloadNothing()
        { 
        }

        public void FromBytes(byte[] data)
        {
        }

        public void FromBytes(byte[] data, int startindex, int size)
        {
        }

        public byte[] ToBytes()
        {
            return EMPTY;
        }

        public int ByteSize
        {
            get { return 0; }
        }
    }


    class PacketPayloadJSON<T> : IPayload where T : class
    {
        T _payload;
        
        public PacketPayloadJSON(T obj)
        {
            _payload = obj;
        }

        public PacketPayloadJSON() 
        {   _payload = null;
        }

        public T Data
        {   get { return _payload; } 
        }

        public void FromBytes(byte[] data)
        {
            FromBytes(data, 0, data == null ? 0 : data.Length);
        }

        public void FromBytes(byte[] data, int startindex, int size)
        {
            if (data == null)
            {   _payload = null;
            }
            else
            {   string str = System.Text.Encoding.ASCII.GetString(data, startindex, size);
                _payload = JsonSerializer.Deserialize<T>(str);

            }
        }

        public byte[] ToBytes()
        {
            return System.Text.Encoding.ASCII.GetBytes(ToJSONString());
        }

        public int ByteSize
        {
            get { return  ToJSONString().Length; }
        }

        string ToJSONString()
        {
            if (_payload == null)
                return string.Empty;
            return JsonSerializer.Serialize(_payload);
        }
    }





}
