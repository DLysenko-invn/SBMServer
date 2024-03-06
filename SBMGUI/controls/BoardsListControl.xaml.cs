using BLE2TCP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    /// Interaction logic for BoardsListControl.xaml
    /// </summary>
    public partial class BoardsListControl:TabContentBaseControl
    {
        public BoardsListControl()
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
            if (e.PropertyName!="Devices")
                return;
            

            _listctrl.ItemsSource = _core.Status.Devices;
            

        }


        private void ButtonScan_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(_core!=null);

            _core.StartWatcher();

            _listctrl.ItemsSource = _core.Status.Devices;

        }

        void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = ((FrameworkElement) e.OriginalSource).DataContext as IDeviceInfo;
            if (item != null)
            {
                Clipboard.SetText(item.Alias);
            }
        }


    }
}
