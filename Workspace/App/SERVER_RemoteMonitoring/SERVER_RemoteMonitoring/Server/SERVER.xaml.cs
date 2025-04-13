using System;
using System.Collections.Generic;
using System.IO;
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
using static SERVER_RemoteMonitoring.Server.SERVER;

namespace SERVER_RemoteMonitoring.Server
{
    /// <summary>
    /// Interaction logic for SERVER.xaml
    /// </summary>
    public partial class SERVER : Window
    {
        public SERVER()
        {
            InitializeComponent();
            List<User> users = new List<User>
            {
                new User { ID = 1, UserName = "Alice", Email = "alice@example.com", IP = "192.168.1.2", Port = "8080", Role = "Connect", ConnectWith = "None", Details = "Alice querry to Bob." },
                new User { ID = 2, UserName = "Bob", Email = "bob@example.com", IP = "192.168.1.3", Port = "8081", Role = "Be connected", ConnectWith = "Alice", Details = "Bob connected to mornitoring Char." },
                new User { ID = 3, UserName = "Charlie", Email = "charlie@example.com", IP = "192.168.1.4", Port = "8082", Role = "Be connected", ConnectWith = "Bob", Details = "Charlie offline." }
            };

            // Tạo danh sách logs, mỗi log gán đúng user từ danh sách
            List<Log> logs = new List<Log>
            { 
                new Log
                {
                    ID=1,
                    LogID = "1",
                    Action = "Alice get process list of Bob.",
                    NameResources="file.txt",
                    Times = DateTime.Now,
                    User = users.First(u => u.ID == 1),

                },
                new Log
                {
                    ID = 2,
                    LogID = "2",
                    Action = "Bob connected to monitoring Char.",
                    NameResources="file.txt",

                    Times = DateTime.Now,
                    User = users.First(u => u.ID == 2), 
                },
                new Log
                {
                    ID = 3,
                    LogID = "3",
                    Action = "Charlie screen view Alice.",
                    NameResources="file.txt",

                    Times = DateTime.Now,
                    User = users.First(u => u.ID == 3), 
                }
            };

            foreach (var log in logs)
            {
                log.User.LastAction = log.Times;
                log.User.LastActionDe = log.Action;

            }


            // Gán dữ liệu vào DataGrid
            SettingsDataGrid.ItemsSource = users;
            DashboardDataGrid.ItemsSource = logs;
            UserControlDataGrid.ItemsSource = users;
            ConnectionsDataGrid.ItemsSource = users;
            LogsDataGrid.ItemsSource = logs;


        }

        public class User
        {
            public int ID { get; set; }
            public string UserName { get; set; }

            public string Password { get; set; }         // thêm Password
            public string Email { get; set; }           // thêm Email
            public string IP { get; set; }              // thêm IP
            public string Port { get; set; }            // thêm Port
            public string ConnectWith { get; set; }     // thêm ConnectWith
            public string Role { get; set; }            // điều khiển / bị điều khiển
            public string Details { get; set; }         // mô tả thêm 
            public DateTime LastAction { get; set; }          // trạng thái kết nối
            public string LastActionDe { get; set; }          // trạng thái kết nối


            // Mối quan hệ 1:N với Log
            public List<Log> Logs { get; set; }         // Danh sách các bản ghi nhật ký liên kết với User
        }


        public class Log
        {
            public int ID { get; set; }  // Khóa ngoại liên kết User
            public string LogID { get; set; }
            public string Action { get; set; }
            public string NameResources { get; set; }

            public DateTime Times { get; set; }

            // Điều hướng đến User
            public User User { get; set; }
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


        private void HomeClick_Click(object sender, MouseButtonEventArgs e)
        {
            Home.Visibility = Visibility.Visible;


            UserControl_Table.Visibility = Visibility.Collapsed;
            Connections.Visibility = Visibility.Collapsed;
            Logs.Visibility = Visibility.Collapsed;
            Settings.Visibility = Visibility.Collapsed;

        }
        private void UserControl_Click(object sender, MouseButtonEventArgs e)
        {
            Home.Visibility = Visibility.Collapsed;
            Connections.Visibility = Visibility.Collapsed;
            Settings.Visibility = Visibility.Collapsed;
            Logs.Visibility = Visibility.Collapsed;

            UserControl_Table.Visibility = Visibility.Visible;
        }
        private void ConenctionsControl_Click(object sender, MouseButtonEventArgs e)
        {
            UserControl_Table.Visibility = Visibility.Collapsed;
            Home.Visibility = Visibility.Collapsed;
            Settings.Visibility = Visibility.Collapsed;
            Logs.Visibility = Visibility.Collapsed;

            Connections.Visibility = Visibility.Visible;
        }
        private void LogsControl_Click(object sender, RoutedEventArgs e)
        {
            UserControl_Table.Visibility = Visibility.Collapsed;
            Home.Visibility = Visibility.Collapsed;
            Connections.Visibility = Visibility.Collapsed;
            Settings.Visibility = Visibility.Collapsed;

            Logs.Visibility = Visibility.Visible;
        }

        private void SettingsControl_Click(object sender, RoutedEventArgs e)
        {
            UserControl_Table.Visibility = Visibility.Collapsed;
            Home.Visibility = Visibility.Collapsed;
            Connections.Visibility = Visibility.Collapsed;
            Logs.Visibility = Visibility.Collapsed;

            Settings.Visibility = Visibility.Visible;
            UserNameTextBox.Clear();
            EmailTextBox.Clear();
            IPTextBox.Clear();
            PortTextBox.Clear();
            PermissionTextBox.Clear();

            TextRange LogsText = new TextRange(LogsTextBox.Document.ContentStart, LogsTextBox.Document.ContentEnd);
            LogsText.Text ="";
        }

        //Setting
        // Bắt sự kiện khi chọn user trong DataGrid
        private void SettingsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsDataGrid.SelectedItem is User selectedUser)
            {
                UserNameTextBox.Text = selectedUser.UserName;
                EmailTextBox.Text = selectedUser.Email;
                IPTextBox.Text = selectedUser.IP;
                PortTextBox.Text = selectedUser.Port;
                PermissionTextBox.Text = selectedUser.Role+ " : " + selectedUser.ConnectWith ;


                TextRange LogsText = new TextRange(LogsTextBox.Document.ContentStart, LogsTextBox.Document.ContentEnd);
                LogsText.Text= selectedUser.Details;

            }
            
        }

        
    }
}
