using System;
using System.Collections.Generic;
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

        private void ButtonScan_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(_core!=null);

            _core.StartWatcher();
        }
    }
}
