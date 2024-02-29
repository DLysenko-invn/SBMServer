using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SBMGUI
{




    public  class AppSettingsData
    {

        public const int FALSE = 0;

        public AppSettingsData()
        {
            IPAddress = "127.0.0.1";
            Port  = 65432;
 
        }

        public string IPAddress     { get;set;} 
        public int   Port           { get;set;} 

    }


    class AppSettings
    {
        AppSettingsData _data = new AppSettingsData();
        string _filename;

        public AppSettings(string filename)
        {
            _filename = filename;
        }


        public AppSettingsData Data
        {   get{    return _data;}
        }

        public void Load()
        {
            string jsonString = File.ReadAllText(_filename);
            _data = JsonSerializer.Deserialize<AppSettingsData>(jsonString);
        }

        public void MakeDefault()
        {
            _data = new AppSettingsData();
            Save();
        }


        public void Save()
        {
            JsonSerializerOptions opt = new JsonSerializerOptions(){WriteIndented = true};
            string jsonString = JsonSerializer.Serialize(_data,opt );
            File.WriteAllText(_filename, jsonString);
        }
        

    }


}
