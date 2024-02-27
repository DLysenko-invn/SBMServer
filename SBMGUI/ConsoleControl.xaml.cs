using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace SBMGUI
{
    /// <summary>
    /// Interaction logic for ConsoleControl.xaml
    /// </summary>
    public partial class ConsoleControl:UserControl,IConsoleOut
    {

        ConsoleContent _dc = new ConsoleContent();


        public ConsoleControl()
        {
            InitializeComponent();
            DataContext = _dc;
        }


        public void Print(string s)
        {
            if (s==null)
                return;
            _dc.ConsoleOutput.Add(s);
            _scroller.ScrollToBottom();
        
        }


    }













    public class ConsoleContent : INotifyPropertyChanged
    {
        ObservableCollection<string> consoleOutput = new ObservableCollection<string>() {  };

        public ObservableCollection<string> ConsoleOutput
        {
            get
            {
                return consoleOutput;
            }
            set
            {
                consoleOutput = value;
                OnPropertyChanged("ConsoleOutput");
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
        {
            if (null != PropertyChanged)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }


}
