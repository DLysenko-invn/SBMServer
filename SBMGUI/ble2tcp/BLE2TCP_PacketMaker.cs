using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BLE2TCP
{

    class PayloadResponse
    {


        public PayloadResponse(ResponseCode c, string s)
        {   this.IntResponseCode = (int)c;
            this.Text  = s;
        }
        public PayloadResponse(ResponseCode c, string s, string serv_uuid,string char_uuid):this(c,s)
        {
            this.ServiceId = serv_uuid;
            this.CharacteristicId = char_uuid;
        }
        public PayloadResponse(ResponseCode c, string s, string devid) : this(c, s)
        {
            this.DeviceId = devid;

        }

        public PayloadResponse(ResponseCode c):this(c,string.Empty)
        {
        }
        [JsonPropertyName("code")]
        public int IntResponseCode { get; }
        [JsonPropertyName("text")]
        public string Text { get; }
        [JsonPropertyName("service")]
        public string ServiceId { get;  }
        [JsonPropertyName("characteristic")]
        public string CharacteristicId { get;  }
        
        [JsonPropertyName("id")]
        public string DeviceId { get; }
    }


    class PayloadDeviceInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }


    class PayloadServerParam
    {
        [JsonPropertyName("name")]
        public string name { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

    }

    class PayloadSC
    {
        [JsonPropertyName("service")]
        public string ServiceId { get; set; }

        [JsonPropertyName("characteristic")]
        public string CharacteristicId { get; set; }

    }





    enum ResponseCode
    { 
        Ok              = 0 ,
        AlreadyStarted  = 1 ,
        Error           = 100,
        ErrorFormat     = 101,
        ErrorNotFound   = 102,
        ErrorBLE        = 103,
    }


    static class PM
    {
        public static PacketBase Indication(PacketOpCode opcode)
        { 
            return  new PacketBase(new PacketHeader(false, PacketOpType.indication,opcode,PacketHeader.INDEX_NONE, PacketHeader.INDEX_NONE ));    
        }
        /*
        public static PacketBase DeviceFound(BLEDeviceInfo i)
        {
            
            PacketPayloadJSON<BLEDeviceInfo> p = new PacketPayloadJSON<BLEDeviceInfo>(i);
            PacketHeader h = new PacketHeader(false, PacketOpType.indication, PacketOpCode.device_found, PacketHeader.INDEX_NONE, PacketHeader.INDEX_NONE);

            return  new PacketBase(h, p);    
        }
*/
        public static PacketBase DeviceFound<T>(T i) where T : class
        {
            
            PacketPayloadJSON<T> p = new PacketPayloadJSON<T>(i);
            PacketHeader h = new PacketHeader(false, PacketOpType.indication, PacketOpCode.device_found, PacketHeader.INDEX_NONE, PacketHeader.INDEX_NONE);

            return  new PacketBase(h, p);    
        }



        public static PacketBase Response<T>(bool iserror,PacketOpCode opcode, T payload) where T : class
        {
            PacketPayloadJSON<T> p = new PacketPayloadJSON<T>(payload);
            PacketHeader h = new PacketHeader(iserror, PacketOpType.response, opcode, PacketHeader.INDEX_NONE, PacketHeader.INDEX_NONE);
            return new PacketBase(h,p);
        }


        public static PacketBase ResponseError(PacketBase p, string text = null)
        {
            return ResponseError(p, ResponseCode.Error, text);
        }


        public static PacketBase ResponseErrorSC(PacketBase p, ResponseCode errorcode, string text, string serv_uuid, string char_uuid )
        {
            PacketHeader h = new PacketHeader(true, PacketOpType.response, p.Header.Code, p.Header.DevIndex, p.Header.SCIndex);
            PacketPayloadJSON<PayloadResponse> d = new PacketPayloadJSON<PayloadResponse>(new PayloadResponse(errorcode, text ?? string.Empty, serv_uuid, char_uuid));
            return new PacketBase(h, d);
        }

        public static PacketBase ResponseError(PacketBase p, ResponseCode errorcode, string text)
        {
            PacketHeader h = new PacketHeader(true, PacketOpType.response, p.Header.Code, p.Header.DevIndex, p.Header.SCIndex );
            PacketPayloadJSON<PayloadResponse> d = new PacketPayloadJSON<PayloadResponse>(new PayloadResponse(errorcode, text ?? string.Empty));
            return new PacketBase(h, d);
        }

        public static PacketBase ResponseError(PacketOpCode opcode, int devindex, int scindex,string text)
        {
            PacketHeader h = new PacketHeader(true, PacketOpType.response, opcode, devindex, scindex);
            if (text==null)
                return new PacketBase(h);
            PacketPayloadJSON<PayloadResponse> p = new PacketPayloadJSON<PayloadResponse>( new PayloadResponse( ResponseCode.Error, text) );
            return new PacketBase(h,p);

        }

        public static PacketBase ResponseOkSC(PacketBase p, string text, string serv_uuid, string char_uuid, ResponseCode responsecode = ResponseCode.Ok)
        {
            PacketPayloadJSON<PayloadResponse> d = new PacketPayloadJSON<PayloadResponse>(new PayloadResponse(responsecode, text, serv_uuid,  char_uuid));
            PacketHeader h = new PacketHeader(false, PacketOpType.response, p.Header.Code, p.Header.DevIndex, p.Header.SCIndex);
            return new PacketBase(h, d);

        }
        public static PacketBase ResponseOkSC(PacketBase p, int scindex, string serv_uuid, string char_uuid, ResponseCode responsecode = ResponseCode.Ok)
        {
            PacketPayloadJSON<PayloadResponse> d = new PacketPayloadJSON<PayloadResponse>(new PayloadResponse(responsecode, string.Empty, serv_uuid, char_uuid));
            PacketHeader h = new PacketHeader(false, PacketOpType.response, p.Header.Code, p.Header.DevIndex, scindex);
            return new PacketBase(h, d);

        }


        public static PacketBase ResponseOkDev(PacketBase p, int devindex,string text,string devid,  ResponseCode responsecode = ResponseCode.Ok)
        {
            PacketPayloadJSON<PayloadResponse> d = new PacketPayloadJSON<PayloadResponse>(new PayloadResponse(responsecode, text, devid));
            PacketHeader h = new PacketHeader(false, PacketOpType.response, p.Header.Code, devindex, p.Header.SCIndex);
            return new PacketBase(h, d);

        }

        public static PacketBase ResponseOk(PacketBase p, string text, ResponseCode responsecode = ResponseCode.Ok )
        {
            PacketPayloadJSON<PayloadResponse> d = new PacketPayloadJSON<PayloadResponse>(new PayloadResponse(responsecode, text));
            PacketHeader h = new PacketHeader(false, PacketOpType.response, p.Header.Code, p.Header.DevIndex, p.Header.SCIndex);
            return new PacketBase(h, d);

        }

        public static PacketBase ResponseOk(PacketBase p)
        {
            PacketHeader h = new PacketHeader(false, PacketOpType.response, p.Header.Code, p.Header.DevIndex, p.Header.SCIndex);
            return new PacketBase(h);

        }
        public static PacketBase ResponseOk(PacketBase p, int scindex)
        {
            PacketHeader h = new PacketHeader(false, PacketOpType.response, p.Header.Code, p.Header.DevIndex, scindex);
            return new PacketBase(h);
        }

        public static PacketBase ResponseOk(PacketOpCode opcode, int devindex, int scindex)
        {
            PacketHeader h = new PacketHeader(false, PacketOpType.response, opcode, devindex, scindex);
            return new PacketBase(h);

        }


        public static PacketBase BLEDataIndication(PacketOpCode opcode, int devindex, int scindex, byte[] data)
        {
            return BLEData(PacketOpType.indication, opcode, devindex, scindex, data);
        }

        public static PacketBase BLEData(PacketOpType t,PacketOpCode opcode, int devindex, int scindex,byte[] data)
        {
            PacketHeader h = new PacketHeader(false, t, opcode, devindex, scindex);
            PacketPayloadBytes p = (data!=null) ? new PacketPayloadBytes(data) : null;
            return new PacketBase(h,p);

        }


        public static PacketBase BLEDataResponse(PacketBase p, byte[] data)
        {
            return BLEData(PacketOpType.response,p.Header.Code, p.Header.DevIndex, p.Header.SCIndex, data);
        }


        /*
        public static PacketBase Response(bool iserror, PacketOpCode opcode, ResponsePayload payload)
        {
            PacketPayloadJSON<ResponsePayload> p = new PacketPayloadJSON<ResponsePayload>(payload);
            PacketHeader h = new PacketHeader(iserror, PacketOpType.response, opcode, PacketHeader.INDEX_NONE, PacketHeader.INDEX_NONE);
            return new PacketBase(h, p);
        }
        */
    }
}
