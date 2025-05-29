using RemoteMonitoringApplication.Services;
using RemoteMonitoringApplication.ViewModels;
using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Org.BouncyCastle.Math;
using static System.Net.Mime.MediaTypeNames;
namespace RemoteMonitoringApplication.Views
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Client : Window
    {
        bool connected = false;
        private List<User> users;
        public ObservableCollection<ProcessInfo> Processes { get; set; }
        private string clientId;
        private string clientPassword;

        private CClient tcpClient;

        private string role;
        private string targetId;

        private ShareScreenService _shareScreen = new ShareScreenService();
        private readonly SystemMonitorViewModel _viewModel = new();
        public Client()
        {
            InitializeComponent();

            tcpClient = SessionManager.Instance.tcpClient;

            if (tcpClient == null)
            {
                System.Windows.MessageBox.Show("⚠️ tcpClient chưa được khởi tạo!", "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            clientId = ClientIdentity.GenerateRandomId();
            clientPassword = ClientIdentity.GenerateRandomPassword();

            lblYourID.Text = $"{clientId}";
            lblYourPass.Text = $"{clientPassword}";

            tcpClient.MessageReceived += OnServerMessage;
            this.Loaded += Client_Loaded;
            this.Closing += Client_Closing;

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

            DataContext = _viewModel;
        }

        // Khi form tắt thì ngắt các kết nối
        private async void Client_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                await _shareScreen.StopScreenSharingAsync();
            }
            catch (Exception ex)
            {
                // Có thể log hoặc bỏ qua nếu shutdown gấp

            }
        }

        private async void Client_Loaded(object sender, RoutedEventArgs e)
        {
            var registerRoomRequest = new
            {
                command = "register_room",
                id = clientId,
                password = clientPassword
            };

            string registerJson = JsonSerializer.Serialize(registerRoomRequest);
            await tcpClient.SendMessageAsync(registerJson);
            Console.WriteLine("📤 Sent register_room");
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
        public class DriveInfoModel
        {
            public string Caption { get; set; }
            public string FreeSpace { get; set; }
            public string Size { get; set; }
        }

        public class User
        {
            public int ID { get; set; }
            public string UserName { get; set; }

            public string Password { get; set; }         // thêm Password
            public string Email { get; set; }           // thêm Email
            public string IP { get; set; }              // thêm IP
            public string Port { get; set; }            // thêm OS
            public string OS { get; set; }              // thêm ConnectWith
            public string ConnectWith { get; set; }     // điều khiển / bị điều khiển
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
            System.Windows.Application.Current.Shutdown();
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

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            string targetId = txtUser.Text.Trim();
            string targetPassword = txtPassword.Password;
            if (tcpClient != null)
            {
                var joinRoomRequest = new
                {
                    command = "join_room",
                    my_id = clientId,
                    my_password = clientPassword,
                    target_id = targetId,
                    target_password = targetPassword
                };

                string json = System.Text.Json.JsonSerializer.Serialize(joinRoomRequest);
                await tcpClient.SendMessageAsync(json);
            }
            else
            {
                System.Windows.MessageBox.Show("Chưa kết nối WebSocket server.", "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Warning);
                Console.WriteLine("Chưa kết nối WebSocket server.");
                return;
            }
            }


        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            connected = false;
            usrEmail.Content = "";
            usrName.Text = "";
            ptnEmail.Content = "";
            ptnName.Text = "";
        }

        private async void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show($"Role hiện tại: {role ?? "(null)"}");

            if (role == "controller")
            {
                System.Windows.MessageBox.Show($"Join channel để XEM màn hình {targetId}");

                _shareScreen.StartScreenSharingAsync(targetId);

                //var startShareRequest = new
                //{
                //    command = "start_share",
                //    target_id = targetId
                //};
                //string json = JsonSerializer.Serialize(startShareRequest);
                //await tcpClient.SendMessageAsync(json);
            }
            else if (role == "partner") { }
            else
            {
                System.Windows.MessageBox.Show("❌ Vai trò chưa được gán! Không thể thực hiện Play!");
            }
        }

        // Nút dừng share màn hình
        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
        }

        private void lblTaskManager_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Screen.Visibility = Visibility.Collapsed;
            Task_Manager.Visibility = Visibility.Visible;
            Performance.Visibility = Visibility.Collapsed;
        }

        private async void btnTaskSync_Click(object sender, RoutedEventArgs e)
        {
            ////System.Windows.MessageBox.Show("Click Sync Task Manager");
            //Console.WriteLine("Click Sync");
            //TextBoxDetails.Document.Blocks.Clear(); ;
            ////string[] Info = _viewModel.FetchAllInfo();
            //var DiskIn4 = _viewModel.diskInfo(_viewModel.FetchDiskInfo());

            //double freeSpace = 0;
            //double size = 0;
            //foreach (var drive in DiskIn4)
            //{
            //    freeSpace += double.Parse(drive.FreeSpace);
            //    size += double.Parse(drive.Size);
            //}

            //double used = 100 - (freeSpace / size * 100);

            //diskBar.Value = 0;
            //for (double i = 0; i <= used; i++)
            //{
            //    diskBar.Value = i;
            //    diskText.Text=$"{i}%";

            //    await Task.Delay(50);
            //}
            
            if (tcpClient != null)
            {
                var SyncRequest = new
                {
                    command = "want_sync",
                    id = clientId,
                    target_id = targetId
                };
                string json = JsonSerializer.Serialize(SyncRequest);
                await tcpClient.SendMessageAsync(json);
                Console.WriteLine("Sent Sync request to server:");
            }
            else
            {
                System.Windows.MessageBox.Show("Chưa kết nối WebSocket server.", "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Warning);
                Console.WriteLine("Chưa kết nối WebSocket server.");
                return;
            }
        }
        private void ShowDiskInfo()
        {
            TextBoxDetails.Document.Blocks.Clear();

            TextBoxDetails.AppendText("Caption\tFreeSpace\tSize\n");
            var DiskIn4 = _viewModel.diskInfo(_viewModel.FetchDiskInfo());

            foreach (var drive in DiskIn4)
            {
                TextBoxDetails.AppendText($"\n{drive.Caption}\t{drive.FreeSpace}\t{drive.Size}");
            }
            Run Text = new Run("\nMORE INFORMATIONS:\n");
            Text.FontWeight = FontWeights.Bold;
            string moreDiskIn4 = _viewModel.fetchIn4("wmic diskdrive get Name,Model,Size,Status\r\n");

            string partitionin4 = _viewModel.fetchIn4("wmic partition get Name,Size,Type\r\n");
            Run Text1 = new Run("Disk drive information:\n");
            Text1.FontWeight = FontWeights.Bold;
            Run Text2 = new Run("Partition information:\n");
            Text2.FontWeight = FontWeights.Bold;

            Paragraph Full = new Paragraph();
            Full.Inlines.Add(Text);
            Full.Inlines.Add(Text1);
            Full.Inlines.Add(moreDiskIn4);
            Full.Inlines.Add(Text2);
            Full.Inlines.Add(partitionin4);
            TextBoxDetails.Document.Blocks.Add(Full);

        }

        private void btnPerformanceSync_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void OnServerMessage(string message)
        {
            await Dispatcher.Invoke(async () =>
            {
                try
                {
                    var json = JsonDocument.Parse(message);
                    var root = json.RootElement;

                    if (!root.TryGetProperty("status", out var statusProp) ||
                        !root.TryGetProperty("command", out var commandProp))
                        return;

                    string status = statusProp.GetString();
                    string command = commandProp.GetString();

                    if (status == "success" && command == "join_room")
                    {
                        if (root.TryGetProperty("user", out var userProp) &&
                            root.TryGetProperty("partner", out var partnerProp))
                        {
                            var user = JsonSerializer.Deserialize<UserInfo>(userProp.GetRawText());
                            var partner = JsonSerializer.Deserialize<UserInfo>(partnerProp.GetRawText());

                            usrEmail.Content = user.email;
                            usrName.Text = user.username;
                            ptnEmail.Content = partner.email;
                            ptnName.Text = partner.username;
                            role = "controller";
                            targetId = partner.id; // SỬA: lấy id, không phải username
                        }
                        connected = true;
                        System.Windows.MessageBox.Show("✅ Join room thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        Remote.Visibility = Visibility.Visible;
                    }
                    else if (status == "info" && command == "partner_joined")
                    {
                        if (root.TryGetProperty("user", out var userProp) &&
                            root.TryGetProperty("partner", out var partnerProp))
                        {
                            var user = JsonSerializer.Deserialize<UserInfo>(userProp.GetRawText());
                            var partner = JsonSerializer.Deserialize<UserInfo>(partnerProp.GetRawText());

                            usrEmail.Content = partner.email;
                            usrName.Text = partner.username;
                            ptnEmail.Content = user.email;
                            ptnName.Text = user.username;
                            role = "partner";
                            targetId = user.id; // SỬA: lấy id, không phải username
                        }
                        System.Windows.MessageBox.Show("✅ Partner join room thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        Home_2.Visibility = Visibility.Visible;
                    }
                    else if (command == "start_share" && status == "info")
                    {
                        role = "partner";
                        var from_id = root.GetProperty("targetId").GetString();
                        var sdp = root.GetProperty("sdp").GetString();
                        var sdpType = root.GetProperty("sdpType").GetString();


                        if (sdpType == "offer")
                        {
                            await _shareScreen.HandleIncomingOffer(sdp, targetId);
                        }
                        else if (sdpType == "answer")
                        {
                            await _shareScreen.HandleIncomingAnswer(sdp, targetId);
                        }
                        else
                        {
                            Console.WriteLine($"❌ SDP type không hợp lệ: {sdpType}");

                        }
                    }
                    else if (command == "ice_candidate" && status == "info")
                    {
                        await _shareScreen.HandleIncomingIceCandidate(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi xử lý message: {ex.Message}");
                }
            });
        }


        private void copyIconID_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Clipboard.SetText(lblYourID.Text);
        }

        private void copyIconPass_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Clipboard.SetText(lblYourPass.Text);
        }

        public class UserInfo
        {
            public string id { get; set; }
            public string username { get; set; }
            public string email { get; set; }
        }

        

        private void btnGetDetail_Click(object sender, RoutedEventArgs e)
        {
            if (ComboBox_Getin4option.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedValue = selectedItem.Content.ToString();
                switch (selectedValue)
                {
                    case "Disk":
                        ShowDiskInfo();
                        break;
                    //case "CPU":
                    //    TextBoxDetails.Document.Blocks.Clear();
                    //    TextBoxDetails.AppendText(_viewModel.FetchCPUInfo());
                    //    break;
                    //case "GPU":
                    //    TextBoxDetails.Document.Blocks.Clear();
                    //    TextBoxDetails.AppendText(_viewModel.FetchGPUInfo());
                    //    break;
                    //case "Memory":
                    //    TextBoxDetails.Document.Blocks.Clear();
                    //    TextBoxDetails.AppendText(_viewModel.FetchMemoryInfo());
                    //    break;
                    default:
                        TextBoxDetails.Document.Blocks.Clear();
                        TextBoxDetails.AppendText("Infomation not found!");
                        break;
                }
            }
        }

        

            
        }
    
}
