using System;
using System.Collections.Generic;
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
                new User { ID = 1, UserName = "Alice", Email = "alice@example.com", IP = "192.168.1.2", Port = "8080", Role = "Connect", ConnectWith = "None", Details = "Alice get process list of Bob." },
                new User { ID = 2, UserName = "Bob", Email = "bob@example.com", IP = "192.168.1.3", Port = "8081", Role = "Be connected", ConnectWith = "Alice", Details = "Bob connected to mornitoring Char." },
                new User { ID = 3, UserName = "Charlie", Email = "charlie@example.com", IP = "192.168.1.4", Port = "8082", Role = "Be connected", ConnectWith = "Bob", Details = "Charlie offline." }
            };

            // Gán dữ liệu vào DataGrid
            SettingsDataGrid.ItemsSource = users;
        }
        public class User
        {
            public int ID { get; set; }
            public string UserName { get; set; }


            public string Email { get; set; }           // thêm Email
            public string IP { get; set; }              // thêm IP
            public string Port { get; set; }            // thêm Port
            public string ConnectWith { get; set; }     // thêm ConnectWith
            public string Role { get; set; }            // điều khiển / bị điều khiển
            public string Details { get; set; }         // mô tả thêm (Logs)

        }
        public class Log
        {
            public string ID { get; set; }
            public string LogID { get; set; }

            public string Action { get; set; }
            public DateTime Times { get; set; }
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
