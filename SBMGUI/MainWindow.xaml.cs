
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

namespace SBMGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow:Window
    {



//        TaskbarIcon _tbi;

        AppCore _core;

        public MainWindow()
        {
            InitializeComponent();

            /*
                Icon i = new System.Drawing.Icon("Error.ico");
                //Icon i = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Error.ico"));
                _tbi = new TaskbarIcon();
                _tbi.Icon = i;
                _tbi.ToolTipText = "hello world";

            */


            this.Loaded+=MainWindow_Loaded;
            this.Closed+=MainWindow_Closed;







            

        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _core?.Dispose();
            _core = null;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _core = new AppCore( new ConsoleLog( _consoletab ) );

            _servertab.SetCore(_core);
            _boardstab.SetCore(_core);


            _core.OnMainWindowLoaded();





        }


    }
}
