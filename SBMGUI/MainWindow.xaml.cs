
using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using BLE2TCP;
using DynamicProtocol;

namespace SBMGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow:Window
    {



//        TaskbarIcon _tbi;

        AppCore _core;
        System.Windows.Forms.NotifyIcon _trayicon;
        System.Drawing.Icon _iconstop,_iconwaiting,_iconconnected,_iconwarning;

        public MainWindow()
        {

/*
            ISDPFrame f = new ISDPFrame();
            byte[] b = new byte[]{
                0x55    ,
                0xAA    ,
                0x02    ,
                0x00    ,
                0x43    ,
                0xFE    ,

                };
            
            while(true)
            { 
                int bytesbonsumed = f.Put(b);
                if (bytesbonsumed==b.Length)
                    break;
                b = b.Skip(bytesbonsumed).ToArray();
            }

            ISDPCmd c = new ISDPCmd(f);

            int etype = c.ETYPE;
            int eid = c.EID;
            int gid = c.GID;
            

*/




            InitializeComponent();

            _iconstop      = LoadIcon("SBMGUI.images.close.ico");
            _iconwaiting   = LoadIcon("SBMGUI.images.checked.ico");
            _iconconnected = LoadIcon("SBMGUI.images.play.ico");
            _iconwarning   = LoadIcon("SBMGUI.images.warning.ico");

            _trayicon = new System.Windows.Forms.NotifyIcon();
            _trayicon.Icon = _iconwarning ;
            _trayicon.Visible = true;
            _trayicon.DoubleClick += 
                delegate(object sender, EventArgs args)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                };



            this.Loaded+=MainWindow_Loaded;
            this.Closed+=MainWindow_Closed;

            


            

        }

        System.Drawing.Icon LoadIcon(string path)
        {
            System.IO.Stream st;
            System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
            st = a.GetManifestResourceStream(path);
            Debug.Assert(st!=null);
            return  new System.Drawing.Icon(st); 
        }


        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Minimized)
                this.Hide();

            base.OnStateChanged(e);
        }




        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _core?.Dispose();
            _core = null;

            _trayicon.Dispose();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _core = new AppCore( new ConsoleLog( _consoletab ) );

            _servertab.SetCore(_core);
            _boardstab.SetCore(_core);


            _core.OnMainWindowLoaded();


            _core.Status.PropertyChanged+=delegate(object s, PropertyChangedEventArgs e){    this.Dispatcher.Invoke(()=>{ Status_PropertyChanged(s, e); }); };

            



        }


        private void Status_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {

            if (e.PropertyName=="ConnectionsCount")
                if (_core.Status.ConnectionsCount == IServerStatus.SERVER_STOPPED)
                {   _trayicon.Icon = _iconstop ;
                } else
                if (_core.Status.ConnectionsCount == 0)
                {   _trayicon.Icon = _iconwaiting ;
                } else
                {   _trayicon.Icon = _iconconnected;
                }

        }



    }
}
