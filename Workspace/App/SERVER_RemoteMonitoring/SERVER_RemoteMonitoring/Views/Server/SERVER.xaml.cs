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
using SERVER_RemoteMonitoring.Data;
namespace SERVER_RemoteMonitoring.Server
{
    /// <summary>
    /// Interaction logic for SERVER.xaml
    /// </summary>
    public partial class SERVER : Window
    {
        private readonly DatabaseService _dbService;

        public SERVER(DatabaseService dbService)
        {
            InitializeComponent();
            _dbService = dbService;

            this.Loaded += SERVER_Loaded; // Gán sự kiện loaded khi cửa sổ mở lên
        }

        private async void SERVER_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            var db = _dbService.GetDataBaseConnection();

            // Lấy dữ liệu logs từ database
            List<Models.User> User = await db.Table<Models.User>().ToListAsync();

            List<Models.Log> sampleLogs = await db.Table<Models.Log>().ToListAsync();
            List<Models.Connections> Connections = await db.Table<Models.Connections>().ToListAsync();
            List<Models.UserLogin> userLogins = await db.Table<Models.UserLogin>().ToListAsync();



            //foreach (var log in sampleLogs)
            //{
            //    log.User.LastAction = log.Times;
            //    log.User.LastActionDe = log.Action;
            //}

            // Gán dữ liệu vào DataGrid
            SettingsDataGrid.ItemsSource = User;
            DashboardDataGrid.ItemsSource = sampleLogs;
            UserControlDataGrid.ItemsSource = userLogins;
            ConnectionsDataGrid.ItemsSource = Connections;
            LogsDataGrid.ItemsSource = sampleLogs;
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


        private async void HomeClick_Click(object sender, MouseButtonEventArgs e)
        {
            await LoadDataAsync();
            Home.Visibility = Visibility.Visible;


            UserControl_Table.Visibility = Visibility.Collapsed;
            Connections.Visibility = Visibility.Collapsed;
            Logs.Visibility = Visibility.Collapsed;
            Settings.Visibility = Visibility.Collapsed;

        }
        private async void UserControl_Click(object sender, MouseButtonEventArgs e)
        {
            await LoadDataAsync();
            Home.Visibility = Visibility.Collapsed;
            Connections.Visibility = Visibility.Collapsed;
            Settings.Visibility = Visibility.Collapsed;
            Logs.Visibility = Visibility.Collapsed;

            UserControl_Table.Visibility = Visibility.Visible;
        }
        private async void ConenctionsControl_Click(object sender, MouseButtonEventArgs e)
        {
            await LoadDataAsync();
            UserControl_Table.Visibility = Visibility.Collapsed;
            Home.Visibility = Visibility.Collapsed;
            Settings.Visibility = Visibility.Collapsed;
            Logs.Visibility = Visibility.Collapsed;

            Connections.Visibility = Visibility.Visible;
        }
        private async void LogsControl_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            UserControl_Table.Visibility = Visibility.Collapsed;
            Home.Visibility = Visibility.Collapsed;
            Connections.Visibility = Visibility.Collapsed;
            Settings.Visibility = Visibility.Collapsed;

            Logs.Visibility = Visibility.Visible;
        }

        private async void SettingsControl_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            UserControl_Table.Visibility = Visibility.Collapsed;
            Home.Visibility = Visibility.Collapsed;
            Connections.Visibility = Visibility.Collapsed;
            Logs.Visibility = Visibility.Collapsed;

            Settings.Visibility = Visibility.Visible;
            //UserNameTextBox.Clear();
            //EmailTextBox.Clear();
            //IPTextBox.Clear();
            //PortTextBox.Clear();
            //PermissionTextBox.Clear();

            //TextRange LogsText = new TextRange(LogsTextBox.Document.ContentStart, LogsTextBox.Document.ContentEnd);
            //LogsText.Text ="";
        }

        //Setting
        // Bắt sự kiện khi chọn user trong DataGrid
        private void SettingsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if (SettingsDataGrid.SelectedItem is User selectedUser)
            {
                //UserNameTextBox.Text = selectedUser.UserName;
                //EmailTextBox.Text = selectedUser.Email;
                //IPTextBox.Text = selectedUser.IP;
                //PortTextBox.Text = selectedUser.Port;
                //PermissionTextBox.Text = selectedUser.Role+ " : " + selectedUser.ConnectWith ;


                //TextRange LogsText = new TextRange(LogsTextBox.Document.ContentStart, LogsTextBox.Document.ContentEnd);
                //LogsText.Text= selectedUser.Details;

            }
            
        }

        private void DashboardDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
