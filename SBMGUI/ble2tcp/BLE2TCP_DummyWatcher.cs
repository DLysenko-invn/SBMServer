using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLE2TCP
{
    class DummyWatcher:IWatcher
    {
        public bool IsStopped
        {
            get{ return false;}
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}
