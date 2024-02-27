using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection.Emit;
using System.Text;

namespace BLE2TCP
{




    enum PacketOpType : byte
    {
        none = 0,
        indication = 1,
        command = 2,
        response =3
    }

    enum PacketOpCode : byte
    {
        nop	                   = 0   ,
        information	           = 1   ,
        set_param	           = 2   ,
        get_param	           = 3   ,
        run_watcher	           = 4   ,
        stop_watcher           = 5   ,
        device_found	       = 6   ,
        watcher_stopped	       = 7   ,
        connect	               = 8   ,
        disconnect	           = 9   ,
        connected	           = 10  ,
        connection_lost	       = 11  ,
        result	               = 12  ,
        serv_char_found	       = 13  ,
        subscribe	           = 15  ,
        unsubscribe	           = 16  ,
        read	               = 17  ,
        write	               = 18  ,
        notification	       = 19  ,
        pair                   = 20  ,
        unpair                 = 21  ,
        get_sc                 = 22  ,





        max = 31
    }

    class PacketHeader
    {

        
        const byte ERROR_BIT    = 0b10000000;
        const byte ERROR_MASK   = 0b01111111;
        const byte GROUP_BITS   = 0b01100000;
        const byte GROUP_MASK   = 0b10011111;
        const byte CODE_BITS    = 0b00011111;
        const byte CODE_MASK    = 0b11100000;
        const int GRPOUP_BITSHIFT = 5;
        public const byte INDEX_NONE = 0;


        public const int BYTESIZE = 3;

        public PacketHeader():this(false, PacketOpType.none, PacketOpCode.nop, INDEX_NONE, INDEX_NONE)
        {
        }


        public PacketHeader(byte[] b) : this(b, 0)
        { 
        }

        public PacketHeader(byte[] b,int startindex)
        {
            if (b.Length > startindex)
            {
                this.IsError = ((b[startindex] & ERROR_BIT) != 0);
                this.Group = (PacketOpType)((b[startindex] & GROUP_BITS) >> GRPOUP_BITSHIFT);
                this.Code = (PacketOpCode)(b[startindex] & CODE_BITS);
            }  else
            {   this.IsError = true;
                this.Group = PacketOpType.none;
                this.Code = PacketOpCode.nop;
            }
            this._devindex = (b.Length > startindex + 1) ? b[startindex + 1] : INDEX_NONE;
            this._scindex  = (b.Length > startindex + 2) ? b[startindex + 2] : INDEX_NONE;

        }

        public PacketHeader(bool isneedack, PacketOpType group, PacketOpCode code,int devindex,int scindex)
        {
            this.Code = code;
            this.Group = group;
            this.IsError = isneedack;
            this._devindex = (byte)devindex;
            this._scindex = (byte)scindex;
        }

        public byte[] ToBytes()
        {
            byte[] r = new byte[BYTESIZE];
            r[0] = (byte)( (((byte)this.Group) << GRPOUP_BITSHIFT) | ((byte)this.Code) | (this.IsError ? ERROR_BIT : 0) );
            r[1] = _devindex;
            r[2] = _scindex;
            return r;
        }

        public PacketOpCode Code { get; }
        public bool IsError { get; }
        public PacketOpType Group {get;}

        byte _devindex, _scindex;

        public byte DevIndex { get { return _devindex; } }
        public byte SCIndex { get { return _scindex; } }
    }






    class PacketBase
    {

        const int PACKETSIZEBYTES = 2;

        static IPayload EMPTYPAYLOAD = new PacketPayloadNothing();
        static PacketHeader ERRORHEADER = new PacketHeader(true, PacketOpType.none, PacketOpCode.nop, PacketHeader.INDEX_NONE, PacketHeader.INDEX_NONE);

        PacketHeader _header;
        protected IPayload _payload;

        public PacketHeader Header { get { return _header; } }

        public IPayload Payload { get { return _payload; } }

        public int ByteSize
        {
            get { return PacketHeader.BYTESIZE + ( (_payload == null) ? 0 : _payload.ByteSize ); }
        }



        void Init(PacketHeader header, IPayload payload)
        {
            _payload = (payload == null) ? EMPTYPAYLOAD : payload;
            Debug.Assert((_payload.ByteSize + PacketHeader.BYTESIZE) < UInt16.MaxValue);
            _header = header;
        }

        public PacketBase(PacketHeader header,IPayload payload)
        {
            Init(header, payload);
        }

        public PacketBase(PacketHeader header):this(header,null)
        {
        }


        public PacketBase(byte[] data)//size bytes not included
        {

            Debug.Assert(data.Length >= PacketHeader.BYTESIZE);

            if ((data == null) || (data.Length < PacketHeader.BYTESIZE))
            {
                Init(ERRORHEADER, EMPTYPAYLOAD);
            }
            else
            {
                Debug.Assert(data.Length < UInt16.MaxValue);

                PacketHeader h =  new PacketHeader(data);
                IPayload p = null;
                if (data.Length > PacketHeader.BYTESIZE)
                {
                    p = PayloadFactory.MakePayload(h, data, PacketHeader.BYTESIZE, data.Length - PacketHeader.BYTESIZE);
                }

                Init(h, p);
            }
        }


        public byte[] ToBytes()//data with size
        {
            Debug.Assert(ByteSize < UInt16.MaxValue);
            byte[] data = new byte[PACKETSIZEBYTES + ByteSize];
            
            Array.Copy( BitConverter.GetBytes( (UInt16)ByteSize ) , 0, data, 0 , PACKETSIZEBYTES);
            Array.Copy( _header.ToBytes(), 0, data, PACKETSIZEBYTES, PacketHeader.BYTESIZE);
            Array.Copy(_payload.ToBytes(),0, data, PACKETSIZEBYTES + PacketHeader.BYTESIZE, _payload.ByteSize);

            return data;
        }




    }





}
