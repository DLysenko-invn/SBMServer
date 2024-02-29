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
    /// <summary>
    /// Interaction logic for ServerStatusControl.xaml
    /// </summary>
    public partial class ServerStatusControl:TabContentBaseControl
    {
        public ServerStatusControl()
        {
            InitializeComponent();
        }


        public override void SetCore(AppCore core)
        {
            base.SetCore( core );

            core.Status.PropertyChanged+=Status_PropertyChanged;

        }

        private void Status_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            this.Dispatcher.Invoke(()=>{ _portctrl.Text = _core.Status.Port.ToString(); });  

        }
    }
}
