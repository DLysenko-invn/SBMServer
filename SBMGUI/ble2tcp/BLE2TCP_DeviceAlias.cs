using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace BLE2TCP
{




    class DeviceAliasTable: IDeviceAliasTable
    {

        Dictionary<string,string> _tab;
        string _filename;

        public DeviceAliasTable(string filename)
        {
            _filename = filename;
            _tab = new Dictionary<string, string>();
        }


        public bool Check(string alias)
        {   return _tab.ContainsKey(alias);
        }

        public string Get(string alias)
        {   return Check(alias) ? _tab[alias] : null;
        }

        public void Set(string alias,string deviceid)
        {   
            if ((alias==null) || (alias.Length==0) || (deviceid==null) || (deviceid.Length==0))
                return;
            _tab[alias]  = deviceid;
        }


        public bool Load()
        {
            _tab=null;
            try
            { 
                string jsonString = File.ReadAllText(_filename);
                _tab = JsonSerializer.Deserialize< Dictionary<string,string> >(jsonString);
            }
            catch
            {   
            }
            if (_tab==null)
            {   _tab = new Dictionary<string, string>();
                return false;
            }
            return true;


        }

        public bool Save()
        {
            try
            { 
                JsonSerializerOptions opt = new JsonSerializerOptions(){WriteIndented = true};
                string jsonString = JsonSerializer.Serialize(_tab,opt );
                File.WriteAllText(_filename, jsonString);
                return true;
            }
            catch
            {
            }
            return false;

        }

        public void RememberAlias(IDeviceInfo dev)
        {
            Set(dev.Alias,dev.Id);                        
        }
    }
}
