using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SBMGUI
{

    public class TabContentBaseControl:UserControl
    {

        protected AppCore _core;

        public TabContentBaseControl()
        {
            _core= null;

        }

        public virtual void SetCore(AppCore core)
        {   
            _core = core;
        }


    }
}

