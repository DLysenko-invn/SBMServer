using BLE2TCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SBMGUI
{
    class ConsoleLog : ILog
    {

        object _lockobj = new object();
        ConsoleControl _console;


        public ConsoleLog(ConsoleControl ctrl)
        {   
            _console = ctrl;
        }

        public void LogLine(string text)
        {
            lock (_lockobj)
            {
                _console.SafePrint(text);
            }
        }
        public void LogError(string text)
        {
            LogLine("Error:"+text);
        }

        public void LogWarning(string text)
        {
            LogLine("Warning:"+text);
        }

    }
}
