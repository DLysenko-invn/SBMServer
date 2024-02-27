using System;
using System.Collections.Generic;
using System.Text;
using Windows.Devices.Enumeration;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Storage.Streams;
using System.Diagnostics;

namespace BLE2TCP
{




    class BLEIndexer
    {
        class SCindexer
        {

            struct SCpair
            {
                public string servuuid;
                public string charuuid;
            }

            public SCindexer(int index,string devid)
            {
                this.Index = index;
                this.Id = devid;
            }

            public string  Id { get; }
            public int Index { get; }
            int  _csindex = PacketHeader.INDEX_NONE;
            Dictionary<string, Dictionary<string, int>> _scdb_str = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<int, SCpair> _scdb_int = new Dictionary<int, SCpair>();

            public int GetSCIndex(string service_uuid, string characteristic_uuid)
            {
                if (string.IsNullOrEmpty(service_uuid) || string.IsNullOrEmpty(characteristic_uuid))
                    return PacketHeader.INDEX_NONE;

                if (!_scdb_str.ContainsKey(service_uuid))
                    _scdb_str[service_uuid] = new Dictionary<string, int>();

                if (!_scdb_str[service_uuid].ContainsKey(characteristic_uuid))
                {
                    int index = ++_csindex;
                    if (_csindex > byte.MaxValue)
                    {   Debug.Assert(false, "SC index overflow");
                        index = PacketHeader.INDEX_NONE;
                    }
                    _scdb_str[service_uuid][characteristic_uuid] = index;
                    _scdb_int[index] = new SCpair() { servuuid = service_uuid, charuuid = characteristic_uuid };
                    
                }

                return _scdb_str[service_uuid][characteristic_uuid];
            }

            public void Get(int scindex, out string service_uuid, out string characteristic_uuid)
            {
                if ( (scindex==PacketHeader.INDEX_NONE) || (!_scdb_int.ContainsKey(scindex)) )
                {
                    service_uuid = string.Empty;
                    characteristic_uuid = string.Empty;
                } else
                {
                    SCpair p = _scdb_int[scindex];
                    service_uuid = p.servuuid;
                    characteristic_uuid = p.charuuid;

                }
            }


        }


        Dictionary<string, SCindexer> _devdb_str = new Dictionary<string, SCindexer>();
        Dictionary<int, SCindexer> _devdb_int = new Dictionary<int, SCindexer>();


        bool[] _devindexes;

        public BLEIndexer()
        {
            _devindexes = new bool[byte.MaxValue];
            for (int i = 0; i < byte.MaxValue; i++)
                _devindexes[i] = false;
            _devindexes[PacketHeader.INDEX_NONE] = true;

        }

        int GetFreeIndex()
        {
            for (int i = 0; i < byte.MaxValue; i++)
                if (!_devindexes[i])
                {
                    _devindexes[i] = true;
                    return i;
                }

            Debug.Assert(false,"No free indexes left");
            return PacketHeader.INDEX_NONE;
        }

        SCindexer GetIndexer(string devid)
        {
            if (!_devdb_str.ContainsKey(devid))
            {
                int index = GetFreeIndex();
                SCindexer i = new SCindexer(index, devid);
                _devdb_str[i.Id] = i;
                _devdb_int[i.Index] = i;
            }
            return _devdb_str[devid];
        }



        public int GetDevIndex(string devid)
        {
            return GetIndexer(devid).Index;
        }

        public int GetSCIndex(string devid, string service_uuid, string characteristic_uuid)
        {
            return GetIndexer(devid).GetSCIndex(service_uuid, characteristic_uuid);
        }

        /*
        public int GetSCIndex(int devindex, string service_uuid, string characteristic_uuid)
        {
            if (!_devdb_int.ContainsKey(devindex))
                return PacketHeader.INDEX_NONE;
            return _devdb_int[devindex].GetSCIndex(service_uuid, characteristic_uuid);
        }
        */
        public string  GetDevId(int devindex)
        {
            if (!_devdb_int.ContainsKey(devindex))
               return  string.Empty;
            return _devdb_int[devindex].Id;

        }
        public bool Get(int devindex, int scindex, out string devid, out string service_uuid, out string characteristic_uuid)
        {
            if (!_devdb_int.ContainsKey(devindex))
            {
                devid = string.Empty;
                service_uuid = string.Empty;
                characteristic_uuid = string.Empty;
                return false;
            }

            SCindexer i = _devdb_int[devindex];
            devid = i.Id;
            i.Get(scindex, out service_uuid, out characteristic_uuid);

            return true;


        }

        public void RemoveDevId(int devindex)
        {
            Debug.Assert(_devindexes[devindex]);
            Debug.Assert(devindex<=byte.MaxValue);

            string devid = GetDevId(devindex);
            if (!string.IsNullOrEmpty(devid))
            {   _devdb_int.Remove(devindex);
                _devdb_str.Remove(devid);
                _devindexes[devindex] = false;
            }
                

        }

    }








    class BLEDeviceInfo
    {
        public BLEDeviceInfo(DeviceInformation deviceInfoIn)
        {
            DeviceInformation = deviceInfoIn;
        }

        [JsonIgnore]
        public DeviceInformation DeviceInformation { get; private set; }

        [JsonPropertyName("id")]
        public string Id => DeviceInformation.Id;

        [JsonPropertyName("name")]
        public string Name => DeviceInformation.Name;

        [JsonPropertyName("is_paired")]
        public bool IsPaired => DeviceInformation.Pairing.IsPaired;
        [JsonPropertyName("is_connected")]
        public bool IsConnected => (bool?)DeviceInformation.Properties["System.Devices.Aep.IsConnected"] == true;
        [JsonPropertyName("is_connectable")]
        public bool IsConnectable => (bool?)DeviceInformation.Properties["System.Devices.Aep.Bluetooth.Le.IsConnectable"] == true;

        [JsonIgnore]
        public IReadOnlyDictionary<string, object> Properties => DeviceInformation.Properties;


        public void Update(DeviceInformationUpdate deviceInfoUpdate)
        {
            DeviceInformation.Update(deviceInfoUpdate);
        }


    }



    static class BLEUtilities
    {
        /// <summary>
        ///     Converts from standard 128bit UUID to the assigned 32bit UUIDs. Makes it easy to compare services
        ///     that devices expose to the standard list.
        /// </summary>
        /// <param name="uuid">UUID to convert to 32 bit</param>
        /// <returns></returns>
        public static ushort ConvertUuidToShortId(Guid uuid)
        {
            // Get the short Uuid
            var bytes = uuid.ToByteArray();
            var shortUuid = (ushort)(bytes[0] | (bytes[1] << 8));
            return shortUuid;
        }

        /// <summary>
        ///     Converts from a buffer to a properly sized byte array
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static byte[] ReadBufferToBytes(IBuffer buffer)
        {
            var dataLength = buffer.Length;
            var data = new byte[dataLength];
            using (var reader = DataReader.FromBuffer(buffer))
            {
                reader.ReadBytes(data);
            }
            return data;
        }
    }


}
