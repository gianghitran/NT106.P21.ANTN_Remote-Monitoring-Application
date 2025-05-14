using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using System.Windows.Shapes;

namespace RemoteMonitoringApplication.Views
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Client: Window
    {
        bool connected = false;
        private List<User> users;
        public ObservableCollection<ProcessInfo> Processes { get; set; }

        public Client()
        {
            InitializeComponent();
            
            users = new List<User>
            {
                new User { ID = 1, UserName = "Alice", Email = "alice@example.com", IP = "192.168.1.2", Port = "8080", OS = "Windows 10", Role = "Connect", ConnectWith = "None", Details = "Alice querry to Bob." },
                new User { ID = 2, UserName = "Bob", Email = "bob@example.com", IP = "192.168.1.3", Port = "8081", OS = "Windows 10", Role = "Be connected", ConnectWith = "Alice", Details = "Bob connected to mornitoring Char." },
            };
            connected = true;

            Processes = new ObservableCollection<ProcessInfo>
            {
            new ProcessInfo { ProcessName = "Chrome",        Status = "Running", CPU = "12.4s", Memory = "350 MB",  Disk = "5 MB/s",   NetWork = "300 KB/s" },
            new ProcessInfo { ProcessName = "Visual Studio", Status = "Running", CPU = "25.1s", Memory = "1200 MB", Disk = "10 MB/s",  NetWork = "150 KB/s" },
            new ProcessInfo { ProcessName = "Discord",       Status = "Running", CPU = "5.3s",  Memory = "200 MB",  Disk = "1 MB/s",   NetWork = "1 MB/s" },
            new ProcessInfo { ProcessName = "Spotify",       Status = "Running", CPU = "3.8s",  Memory = "150 MB",  Disk = "0.5 MB/s", NetWork = "500 KB/s" },
            new ProcessInfo { ProcessName = "Explorer",      Status = "Running", CPU = "1.2s",  Memory = "100 MB",  Disk = "0.1 MB/s", NetWork = "50 KB/s" }
            };

            // Set DataContext để Binding
            this.DataContext = this;
        }

        public class ProcessInfo
        {
            public string ProcessName { get; set; }
            public string Status { get; set; }
            public string CPU { get; set; }
            public string Memory { get; set; }
            public string Disk { get; set; }
            public string NetWork { get; set; }
        }

        public class User
        {
            public int ID { get; set; }
            public string UserName { get; set; }

            public string Password { get; set; }         // thêm Password
            public string Email { get; set; }           // thêm Email
            public string IP { get; set; }              // thêm IP
            public string Port { get; set; }            // thêm Port
            public string OS { get; set; }              // thêm OS
            public string ConnectWith { get; set; }     // thêm ConnectWith
            public string Role { get; set; }            // điều khiển / bị điều khiển
            public string Details { get; set; }         // mô tả thêm 
            public DateTime LastAction { get; set; }          // trạng thái kết nối
            public string LastActionDe { get; set; }          // trạng thái kết nối
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void lblScreen_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Screen.Visibility = Visibility.Visible;
            Performance.Visibility = Visibility.Collapsed;
            Task_Manager.Visibility = Visibility.Collapsed;
        }

        private void lblPerformance_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Screen.Visibility = Visibility.Collapsed;
            Performance.Visibility = Visibility.Visible;
            Task_Manager.Visibility = Visibility.Collapsed;
        }

        private void btnHome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Home_1.Visibility = Visibility.Visible;
            Home_2.Visibility = Visibility.Visible;
            Remote.Visibility = Visibility.Collapsed;
            
        }

        private void btnRemote_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Home_1.Visibility = Visibility.Collapsed;
            Home_2.Visibility = Visibility.Collapsed;
            Remote.Visibility = Visibility.Visible;
        }

        private void btnHistory_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Home_1.Visibility = Visibility.Visible;
            Home_2.Visibility = Visibility.Visible;
            Remote.Visibility = Visibility.Collapsed;
        }
        
        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (connected == true)
            {
                usrEmail.Content = users[0].Email;
                usrIP.Content = users[0].IP;
                usrOS.Content = users[0].OS;
                ptnEmail.Content = users[1].Email;
                ptnIP.Content = users[1].IP;
                ptnOS.Content = users[1].OS;
            }    
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            connected = false;
            usrEmail.Content = "";
            usrIP.Content = "";
            usrOS.Content = "";
            ptnEmail.Content = "";
            ptnIP.Content = "";
            ptnOS.Content = "";
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {

        }

        private void lblTaskManager_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Screen.Visibility = Visibility.Collapsed;
            Task_Manager.Visibility = Visibility.Visible;
            Performance.Visibility = Visibility.Collapsed;
        }

        private void btnTaskSync_Click(object sender, RoutedEventArgs e)
        {
            

        }

        private void btnPerformanceSync_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
