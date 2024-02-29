using BLE2TCP;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SBMGUI
{
    public class ConsoleLog : ILog
    {

        ConsoleControl _console;


        public ConsoleLog(ConsoleControl ctrl)
        {   
            Debug.Assert(ctrl!=null);
            _console = ctrl;
        }

        public void LogLine(string text)
        {
            _console.Print(text);
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
