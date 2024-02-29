using System;
using System.Collections.Generic;
using System.Text;
using BLE2TCP;

namespace SBMGUI
{
    public class AppCore:IDisposable
    {

        const string CFGFILENAME = "appconfig.json";

        ConsoleLog _log;
        AppSettings _cfg;
        Server _server;


        public AppCore(ConsoleLog log)
        {
            _log = log;          
            _cfg = new AppSettings(CFGFILENAME);
        }

        public void Dispose()
        {
            ServerStop();
        }

        public void ServerStart()
        {
            ServerStop();

            SocketServer t = new SocketServer(_log,_cfg.Data.Port);
            Core c = new Core(_log,t);
            _server = new Server(_log,t,t,c);

            _server.StartAsNewThread();
        }


        public void ServerStop()
        {
            if (_server == null)
                return;

            _server.Stop();
            _server = null;

        }


        public void StartWatcher()
        {

        }

        public void OnMainWindowLoaded()
        {
            ServerStart();
        }
    }
}
