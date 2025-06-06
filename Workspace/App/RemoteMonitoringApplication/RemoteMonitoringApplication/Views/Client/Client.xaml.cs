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
using Microsoft.VisualBasic.Logging;
using static System.Windows.Forms.Design.AxImporter;
using RemoteMonitoringApplication.Services;
using Windows.Media.Protection.PlayReady;
using System.IO;
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
        private SystemMonitorViewModel _viewModel = new();
        private ProcessViewModel _viewModelProcess = new();
        private ProcessDumpViewModel _viewModelPCdump = new ();
        private SharePerformanceInfo _GetInfo = new SharePerformanceInfo();
        private ProcessMonitorService _ProcessSerivce = new ProcessMonitorService();
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

            _shareScreen.OnFrameReceived += (data, timestamp, codec, width, height) =>
            {
                var bitmap = _shareScreen.ConvertToBitmap(data, codec, width, height);
                if (bitmap != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        // Hiển thị hình ảnh lên UI
                        imgAgoraVideo.Source = _shareScreen.BitmapToImageSource(bitmap);
                    });
                }
            };
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
        public class PairID
        {
            public string id { get; set; }
            public string target_id { get; set; }
        }
        public class RequestPCDump
        {
            public string id { get; set; }
            public string target_id { get; set; }
            public string PID { get; set; } // Thêm PID để yêu cầu dump của tiến trình cụ thể
        }
        public class RemoteInfoMessage
        {
            public List<DriveDiskModel> Drives { get; set; }
            public List<DriveMemoryModel> Memory { get; set; }
            public List<DriveCPUModel> CPU { get; set; }

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

                await _shareScreen.StartScreenSharingAsync(targetId);

            }
            else if (role == "partner") { }
            else
            {
                System.Windows.MessageBox.Show("❌ Vai trò chưa được gán! Không thể thực hiện Play!");
            }
        }

        // Nút dừng share màn hình
        private async void btnStop_Click(object sender, RoutedEventArgs e)
        {
            await _shareScreen.StopScreenSharingAsync();
            Dispatcher.Invoke(() =>
            {
                // Dừng hiển thị
                imgAgoraVideo.Source = null;
            });
        }

        private void lblTaskManager_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Screen.Visibility = Visibility.Collapsed;
            Task_Manager.Visibility = Visibility.Visible;
            Performance.Visibility = Visibility.Collapsed;
        }

        private async void btnTaskSync_Click(object sender, RoutedEventArgs e)
        {

            if (tcpClient != null)
            {
                if (role == "controller")
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
                    System.Windows.MessageBox.Show("You are not controller!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Console.WriteLine("You are not controller!");
                    return;
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Disconnected Socket server.", "Connect error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Console.WriteLine("Disconnected Socket server");
                return;
            }
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
                    else if (command == "want_sync" && status == "success")
                    {
                        Console.WriteLine("Received sync request from server");
                        System.Windows.MessageBox.Show("Received sync request from server", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        if (root.TryGetProperty("message", out var mess))
                        {
                            var Pair = JsonSerializer.Deserialize<PairID>(mess.GetRawText());
                            //Console.WriteLine($"Pair ID: {Pair.id}, Target ID: {Pair.target_id}");
                            var Diskinfo = _viewModel.diskInfo(_viewModel.FetchDiskInfo());
                            var Memoryinfo = _viewModel.MemoryInfo(_viewModel.FetchMemoryInfo());
                            var CPUinfo = _viewModel.CPUInfo(_viewModel.FetchCPUInfo());
                            //Console.WriteLine($"Disk info: {Diskinfo.Count} drives, Memory info: {Memoryinfo.Count} items, CPU info: {CPUinfo.Count} items");
                            var Info = new
                            {
                                command = "SentRemoteInfo",
                                info = Diskinfo,
                                infoMemory = Memoryinfo,
                                infoCPU = CPUinfo,
                                Monitor_id = Pair.id,//theo doi
                                Remote_id = Pair.target_id// bị theo dõi ( dự liệu theo dõi là của máy này)
                            };
                            string Infojson = JsonSerializer.Serialize(Info);
                            await tcpClient.SendMessageAsync(Infojson);
                            Console.WriteLine("Sent remote info to server, then to client (monitor) ", Pair.id);
                        }
                        else
                        {
                            Console.WriteLine("Sync error: id and target id not found!");
                        }
                    }
                    else if (command == "SentRemoteInfo" && status == "success")
                    {

                        if (root.TryGetProperty("message", out var Remote_info))
                        {
                            var options = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };

                            var Info = JsonSerializer.Deserialize<RemoteInfoMessage>(Remote_info.GetRawText(), options);

                            //Console.WriteLine($"Pair ID: {Info.Drives}, Target ID: {Info.Memory}");

                            _GetInfo.showDiskBar(Info.Drives, diskBar, diskText);
                            _GetInfo.showMemoryBar(Info.Memory, memoryBar, memoryText);
                            _GetInfo.showCPUBar(Info.CPU, cpuBar, cpuText);



                        }
                        else
                        {
                            Console.WriteLine("Sync error: id and target id not found!");
                        }
                    }
                    else if ((command == "want_diskDetail" || command == "want_CPUDetail" || command == "want_GPUDetail" || command == "want_MemoryDetail") && status == "success")
                    {

                        Console.WriteLine("Received want Detail request from server");
                        System.Windows.MessageBox.Show("Received detail info request from server", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
                        if (root.TryGetProperty("message", out var mess))
                        {
                            var Pair = JsonSerializer.Deserialize<PairID>(mess.GetRawText());
                            //Console.WriteLine($"Pair ID: {Pair.id}, Target ID: {Pair.target_id}");
                            var Data = _viewModel.FetchRawInfo(command);


                            var Info = new
                            {
                                command = "SentDetail",
                                info = Data,

                                Monitor_id = Pair.id,//theo doi
                                Remote_id = Pair.target_id// bị theo dõi ( dự liệu theo dõi là của máy này)
                            };
                            string Infojson = JsonSerializer.Serialize(Info);
                            await tcpClient.SendMessageAsync(Infojson);
                            Console.WriteLine("Sent remote info to server, then to client (monitor) ", Pair.id);
                        }

                        else
                        {
                            Console.WriteLine("Received detail request info error: id and target id not found!");
                        }
                    }
                    else if (command == "SentDetail" && status == "success")
                    {
                        if (root.TryGetProperty("message", out var DataDetail))
                        {
                            var infoDetail = DataDetail.GetString();
                            TextBoxDetails.Document.Blocks.Clear();
                            TextBoxDetails.AppendText(infoDetail);
                        }
                        else
                        {
                            Console.WriteLine("Received detail info error: id and target id not found!");
                        }
                    }
                    else if (command == "want_processList" && status == "success")
                    {

                        Console.WriteLine("Received want process list request from server");
                        System.Windows.MessageBox.Show("Received want process list request from server", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
                        if (root.TryGetProperty("message", out var mess))
                        {
                            var Pair = JsonSerializer.Deserialize<PairID>(mess.GetRawText());
                            //Console.WriteLine($"Pair ID: {Pair.id}, Target ID: {Pair.target_id}");
                            var Data = _ProcessSerivce.getProcessList();
                            Console.WriteLine($"Process list start sending");

                            var Info = new
                            {
                                command = "SentprocessList",
                                info = Data,

                                Monitor_id = Pair.id,//theo doi
                                Remote_id = Pair.target_id// bị theo dõi ( dự liệu theo dõi là của máy này)
                            };
                            string Infojson = JsonSerializer.Serialize(Info);
                            await tcpClient.SendMessageAsync(Infojson);
                            Console.WriteLine("Sent process list to server, then to client (monitor) ", Pair.id);
                        }
                        else
                        {
                            Console.WriteLine("Received detail info error: id and target id not found!");
                        }
                    }
                    else if (command == "SentprocessList" && status == "success")
                        {
                            Console.WriteLine("Received process list from server");
                            System.Windows.MessageBox.Show("Received want process list from server", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);

                            if (root.TryGetProperty("message", out var processList))
                            {

                                var processListObj = JsonSerializer.Deserialize<ProcessList>(processList.GetRawText());
                                Console.WriteLine($"Process list getting");
                            timeGetProcessList.SetValue(System.Windows.Controls.Label.ContentProperty, $"Monitor time:{processListObj.RealTime}");

                            _viewModelProcess.BindProcessListToDataGrid(processListObj, ProcessDataGrid);
                            }
                            else
                            {
                                Console.WriteLine("Sent processList error: id and target id not found!");
                            }
                        }
                    else if (command == "want_processDump" && status == "success")
                        {
                            Console.WriteLine("Received process dump request from server");
                            System.Windows.MessageBox.Show("Received want process dump request from server", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
                            if (root.TryGetProperty("message", out var mess))
                            {
                                var Mess = JsonSerializer.Deserialize<RequestPCDump>(mess.GetRawText());
                                //var Data = _ProcessSerivce.getProcessList();
                                _viewModelPCdump.ProcessDump(Mess.PID);
                            Console.WriteLine($"Process dump start sending.........");

                            //await tcpClient.SendFileAsync("dumpTemp.dmp");
                            await tcpClient.SendFileAsync("dumpTemp.dmp", "SentprocessDump", Mess.id);

                            Console.WriteLine("Sent process dump to server, then to client (monitor) ", Mess.id);
                            }
                            else
                            {
                                Console.WriteLine("Received detail info error: id and target id not found!");
                            }
                        }
                    else if (command == "SentprocessDump" && status == "success" )
                    {
                        if (!root.TryGetProperty("target_id", out var targetIdProp) ||
                            !root.TryGetProperty("length", out var lengthProp))
                        {
                            Console.WriteLine("fail", "SentprocessDump", "Thiếu thông tin cần thiết.");
                            return;
                        }
                        Console.WriteLine("Received process dump from server");
                        System.Windows.MessageBox.Show("Received process dump from server", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
                        string targetId = targetIdProp.GetString();
                        long dumpLength = lengthProp.GetInt64();

                        if (dumpLength <= 0)
                        {
                            Console.WriteLine("fail", "SentprocessDump", "Dump length không hợp lệ.");
                            return;
                        }
                            Console.WriteLine($"Starting saved dump file of {dumpLength} bytes");

                            try
                            {
                            //string path = "dumpReceived.dmp";
                            //if (!Directory.Exists(path))
                            //{
                            //    Directory.CreateDirectory(path);
                            //}
                            string filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Savedata", "dumpReceived.dmp");

                            //await tcpClient.ReceiveAndSaveFileAsync(filePath);
                                Console.WriteLine($"Saved completed. {dumpLength} bytes sent to {targetId}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Relay failed: {ex.Message}");
                                Console.WriteLine("fail", "SentprocessDump", $"Relay failed: {ex.Message}");
                            }
                        
                    }
                    else
                        {
                            Console.WriteLine($"Command not found '{command}' and state '{status}'");
                        }
                    
                        

                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Message process error: {ex.Message}");
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



        private async void btnGetDetail_Click(object sender, RoutedEventArgs e)
        {
            if (role == "controller")
            {
                if (ComboBox_Getin4option.SelectedItem is ComboBoxItem selectedItem)
                {
                    string selectedValue = selectedItem.Content.ToString();
                    switch (selectedValue)
                    {
                        case "Disk":
                            //_GetInfo.GetDiskInfo();
                            if (tcpClient != null)
                            {
                                var DiskDetail = new
                                {
                                    command = "want_diskDetail",
                                    id = clientId,
                                    target_id = targetId
                                };
                                string json = JsonSerializer.Serialize(DiskDetail);
                                await tcpClient.SendMessageAsync(json);
                                Console.WriteLine("Sent diskDetail request to server.");
                            }
                            else
                            {
                                System.Windows.MessageBox.Show("WebSocket server disconnected.", "Connect error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                Console.WriteLine("WebSocket server disconnected.");
                                return;
                            }
                            break;
                        case "CPU":
                            if (tcpClient != null)
                            {
                                var CPUDetail = new
                                {
                                    command = "want_CPUDetail",
                                    id = clientId,
                                    target_id = targetId
                                };
                                string json = JsonSerializer.Serialize(CPUDetail);
                                await tcpClient.SendMessageAsync(json);
                                Console.WriteLine("Sent CPUDetail request to server.");
                            }
                            else
                            {
                                System.Windows.MessageBox.Show("WebSocket server disconnected.", "Connect error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                Console.WriteLine("WebSocket server disconnected.");
                                return;
                            }

                            break;
                        //    TextBoxDetails.Document.Blocks.Clear();
                        //    TextBoxDetails.AppendText(_viewModel.FetchCPUInfo());
                        //    break;
                        case "Memory":
                            if (tcpClient != null)
                            {
                                var MemoryDetail = new
                                {
                                    command = "want_MemoryDetail",
                                    id = clientId,
                                    target_id = targetId
                                };
                                string json = JsonSerializer.Serialize(MemoryDetail);
                                await tcpClient.SendMessageAsync(json);
                                Console.WriteLine("Sent MemoryDetail request to server.");
                            }
                            else
                            {
                                System.Windows.MessageBox.Show("WebSocket server disconnected.", "Connect error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                Console.WriteLine("WebSocket server disconnected.");
                                return;
                            }
                            break;

                        //    TextBoxDetails.Document.Blocks.Clear();
                        //    TextBoxDetails.AppendText(_viewModel.FetchMemoryInfo());
                        //    break;
                        case "GPU":
                            if (tcpClient != null)
                            {
                                var MemoryDetail = new
                                {
                                    command = "want_GPUDetail",
                                    id = clientId,
                                    target_id = targetId
                                };
                                string json = JsonSerializer.Serialize(MemoryDetail);
                                await tcpClient.SendMessageAsync(json);
                                Console.WriteLine("Sent GPUDetail request to server.");
                            }
                            else
                            {
                                System.Windows.MessageBox.Show("WebSocket server disconnected.", "Connect error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                Console.WriteLine("WebSocket server disconnected.");
                                return;
                            }
                            break;
                        default:
                            TextBoxDetails.Document.Blocks.Clear();
                            TextBoxDetails.AppendText("Infomation not found!");
                            break;
                    }
                }
            }

            else
            {
                System.Windows.MessageBox.Show("You are not controller!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Console.WriteLine("You are not controller!");
                return;
            }
        }

        private async void btnProcessList_Click(object sender, RoutedEventArgs e)
        {
            timeGetProcessList.ClearValue(ContentProperty);
            if (role == "controller") { 
                timeGetProcessList.SetValue(System.Windows.Controls.Label.ContentProperty, $"Getting process information...");

                if (tcpClient != null)
                {
                    if (role == "controller")
                    {
                        var SyncRequest = new
                        {
                            command = "want_processList",
                            id = clientId,
                            target_id = targetId
                        };
                        string json = JsonSerializer.Serialize(SyncRequest);
                        await tcpClient.SendMessageAsync(json);
                        Console.WriteLine("Sent processList request to server:");
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("You are not controller!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Console.WriteLine("You are not controller!");
                        return;
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("Disconnected Socket server.", "Connected Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Console.WriteLine("Disconnected Socket server.");
                    return;
                }
            }
        }

        private async void btnProcessDump_Click(object sender, RoutedEventArgs e)
        {
            //byte[] dumpdata = _viewModelPCdump.ProcessDump(Textbox_PID);
            if (role == "controller")
            {
                if (tcpClient != null)
                {
                    if (role == "controller")
                    {
                        var SyncRequest = new
                        {
                            command = "want_processDump",
                            ProcessPID = Textbox_PID.Text.Trim(),
                            id = clientId,
                            target_id = targetId
                        };
                        string json = JsonSerializer.Serialize(SyncRequest);
                        await tcpClient.SendMessageAsync(json);
                        Console.WriteLine("Sent want_processDump request to server:");
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("You are not controller!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Console.WriteLine("You are not controller!");
                        return;
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("Disconnected Socket server.", "Connect error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Console.WriteLine("Disconnected Socket server");
                    return;
                }
            }
        }
    }// public class
    
}// namespace
