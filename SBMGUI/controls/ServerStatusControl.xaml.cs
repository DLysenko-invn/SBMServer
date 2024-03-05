using BLE2TCP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

            core.Status.PropertyChanged+=Status_PropertyChangedUnsafe;

        }

        private void Status_PropertyChangedUnsafe(object sender, PropertyChangedEventArgs e)
        {
            this.Dispatcher.Invoke(()=>{ Status_PropertyChanged(sender, e); });  
        }



        private void Status_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            _portctrl.Text = _core.Status.Port.ToString();
            if (_core.Status.ConnectionsCount == IServerStatus.SERVER_STOPPED)
            {   _conncountctrl.Text = "Stopped";
            } else
            if (_core.Status.ConnectionsCount == 0)
            {   _conncountctrl.Text = "Waiting for connection";
            } else
            {   _conncountctrl.Text = "Connected";
            }
            //_conncountctrl.Text = _core.Status.ConnectionsCount.ToString();
            _rxbytesctrl.Text = _core.Status.RXBytes.ToString();
            _txbytesctrl.Text = _core.Status.TXBytes.ToString();
            

        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            _core.ServerStart();
        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            _core.ServerStop();
        }
    }
}
