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
        private string clientId;
        private string clientPassword;

        private CClient tcpClient;

        private string role;
        private string targetId;

        private ShareScreenService _shareScreen = new ShareScreenService();
        private SystemMonitorViewModel _viewModel = new();
        private ProcessViewModel _viewModelProcess = new();
        private ProcessDumpViewModel _viewModelPCdump = new();
        private CompressService _viewCompress = new();
        private SharePerformanceInfo _GetInfo = new SharePerformanceInfo();
        private ProcessMonitorService _ProcessSerivce = new ProcessMonitorService();
        private string savedTempId;
        private string savedClientPassword;
        private string savedUsername;
        private string savedPassword;
        private readonly AuthService _auth;
        private string OtherPublicKey = "null";
        private string MyPrivKey = "null";
        private string SharedKey = "null";
        private string SuperIV = "nghinghiadavit23";

        public Client(AuthService auth, CClient sharedClient)
        {
            InitializeComponent();
            _auth = auth;
            tcpClient = sharedClient;

            if (tcpClient != null)
            {
                tcpClient.MessageReceived -= OnServerMessage;
                tcpClient.MessageReceived += OnServerMessage;
            }


            if (tcpClient == null)
            {
                System.Windows.MessageBox.Show("⚠️ tcpClient chưa được khởi tạo!", "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Nếu chưa có thông tin tạm, thì tạo mới
            if (string.IsNullOrEmpty(SessionManager.Instance.ClientId) || string.IsNullOrEmpty(SessionManager.Instance.ClientPassword))
            {
                savedTempId = ClientIdentity.GenerateRandomId();
                savedClientPassword = ClientIdentity.GenerateRandomPassword();
                SessionManager.Instance.ClientId = savedTempId;
                SessionManager.Instance.ClientPassword = savedClientPassword;
            }
            else
            {
                savedTempId = SessionManager.Instance.ClientId;
                savedClientPassword = SessionManager.Instance.ClientPassword;
            }

            lblYourID.Text = savedTempId;
            lblYourPass.Text = savedClientPassword;


            // Fix for the errors related to the event handler for `_shareScreen.OnFrameReceived`  
            _shareScreen.OnFrameReceived += (frame, timestamp, codec, width, height) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (frame != null && frame.Length > 0)
                    {
                        // Convert byte[] to BitmapImage  
                        var bitmap = _shareScreen.ConvertToBitmap(frame, codec, width, height);
                        var image = _shareScreen.BitmapToImageSource(bitmap);
                        imgAgoraVideo.Source = image;
                    }
                    else
                    {
                        imgAgoraVideo.Source = null; // Clear image if no frame is available  
                    }
                });
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
            tcpClient.MessageReceived -= OnServerMessage;
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

        private TaskCompletionSource<bool> loginCompletedTcs;
        private TaskCompletionSource<int> partnerPortTcs;
        private TaskCompletionSource<bool> registerRoomCompletedTcs;

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            string targetId = txtUser.Text.Trim();
            string targetPassword = txtPassword.Password;

            int partnerPort = await GetPartnerServerPortAsync(targetId).ConfigureAwait(false);
            int currentPort = tcpClient.Port;
            Console.WriteLine($"Current port: {currentPort}, Partner port: {partnerPort}");

            if (currentPort != partnerPort)
            {
                loginCompletedTcs = new TaskCompletionSource<bool>();
                registerRoomCompletedTcs = new TaskCompletionSource<bool>();

                Console.WriteLine("🧹 Disconnecting...");

                tcpClient.IsReconnecting = true;

                var oldClient = tcpClient; // lưu client cũ
                oldClient.MessageReceived -= OnServerMessage;

                await oldClient.DisconnectAsync();
                tcpClient = new CClient("localhost", partnerPort);
                tcpClient.MessageReceived -= OnServerMessage;
                tcpClient.MessageReceived += OnServerMessage;
                await tcpClient.ConnectAsync();

                SessionManager.Instance.tcpClient = tcpClient;
                savedUsername = SessionManager.Instance.username;
                savedPassword = SessionManager.Instance.password;

                if (string.IsNullOrEmpty(savedUsername) || string.IsNullOrEmpty(savedPassword))
                {
                    System.Windows.MessageBox.Show("⚠️ Không thể đăng nhập lại vì thiếu username/password.", "Lỗi đăng nhập lại", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var loginRequest = new
                {
                    command = "login",
                    username = savedUsername,
                    password = savedPassword
                };
                await tcpClient.SendMessageAsync(savedTempId, savedTempId, loginRequest);
                await loginCompletedTcs.Task;

                var registerRoomRequest = new
                {
                    command = "register_room",
                    id = savedTempId,
                    password = savedClientPassword
                };
                await tcpClient.SendMessageAsync(savedTempId, null, registerRoomRequest);
                await registerRoomCompletedTcs.Task;

                // Chờ nhận được phản hồi thành công từ server
                await registerRoomCompletedTcs.Task;

                // Sau đó mới gửi join_room
                var joinRoomRequest = new
                {
                    command = "join_room",
                    my_id = savedTempId,
                    my_password = savedClientPassword,
                    target_id = targetId,
                    target_password = targetPassword
                };
                await tcpClient.SendMessageAsync(savedTempId, targetId, joinRoomRequest);
            }

            else
            {
                var joinRoomRequest = new
                {
                    command = "join_room",
                    my_id = savedTempId,
                    my_password = savedClientPassword,
                    target_id = targetId,
                    target_password = targetPassword
                };
                await tcpClient.SendMessageAsync(savedTempId, targetId, joinRoomRequest);
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
                //await _shareScreen.StartScreenSharingAsync(targetId);
                var shareOffer = new
                {
                    command = "start_share",
                    type = "offer",
                    targetId = targetId,
                };
                await tcpClient.SendMessageAsync(clientId, targetId, shareOffer);
            }
            else if (role == "partner") { }
            else
            {
                System.Windows.MessageBox.Show("Vai trò chưa được gán! Không thể thực hiện Play!");
            }
        }

        // Nút dừng share màn hình cho Controller
        private async void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (role == "controller")
            {
                //await _shareScreen.StopScreenSharingAsync();
                var stopShareRequest = new
                {
                    command = "stop_share",
                    type = "stop",
                    targetId = targetId
                };

                await tcpClient.SendMessageAsync(clientId, targetId, stopShareRequest);

                Dispatcher.Invoke(() =>
                {
                    // Dừng hiển thị
                    imgAgoraVideo.Source = null;
                });
            }
            else if (role == "partner")
            {
                System.Windows.MessageBox.Show("Only controller can stop share screen");
            }
        }

        private void lblTaskManager_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Screen.Visibility = Visibility.Collapsed;
            Task_Manager.Visibility = Visibility.Visible;
            Performance.Visibility = Visibility.Collapsed;
        }

        private async void btnTaskSync_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine($"[SYNC] role={role}, clientId={clientId}, targetId={targetId}");
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
                    await tcpClient.SendMessageAsync(clientId, targetId, SyncRequest);
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

        public async void OnServerMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || !message.TrimStart().StartsWith("{"))
            {
                Console.WriteLine($"[WARN] Received non-JSON message: {message}");
                return;
            }

            var json = JsonDocument.Parse(message);
            var root = json.RootElement;
            string toId = root.TryGetProperty("to", out var toProp) ? toProp.GetString() : null;

            // Nếu tcpClient.Id chưa có (null hoặc rỗng), gán luôn bằng toId
            if (string.IsNullOrEmpty(tcpClient.Id) && !string.IsNullOrEmpty(toId))
            {
                tcpClient.Id = toId;
                SessionManager.Instance.ClientId = toId;
                Console.WriteLine($"[SYNC] Set tcpClient.Id = {toId}");
            }

            // Nếu message không gửi cho client này thì bỏ qua
            if (!string.IsNullOrEmpty(toId) && toId != tcpClient.Id)
            {
                Console.WriteLine($"Message not for this client (to: {toId}, my id: {tcpClient.Id}). Ignoring.");
                return;
            }

            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("OnServerMessage received: " + message);

            try
            {
                // Xử lý trường hợp KHÔNG có "payload"
                if (root.TryGetProperty("command", out var commandProp) &&
                    root.TryGetProperty("status", out var statusProp) &&
                    commandProp.GetString() == "get_partner_port" &&
                    statusProp.GetString() == "success" &&
                    root.TryGetProperty("port", out var portProp))
                {
                    int port = portProp.GetInt32();
                    Console.WriteLine($"✅ Received partner port (no payload): {port}");
                    partnerPortTcs?.TrySetResult(port);
                    return;
                }

                // Xử lý trường hợp CÓ "payload" (giữ lại cho các message khác)
                if (root.TryGetProperty("payload", out var payload))
                {
                    string status = payload.GetProperty("status").GetString();
                    string command = payload.GetProperty("command").GetString();

                    if (command == "get_partner_port" && status == "success")
                    {
                        if (payload.TryGetProperty("port", out var portProp2))
                        {
                            int port = portProp2.GetInt32();
                            Console.WriteLine($"✅ Received partner port (payload): {port}");
                            partnerPortTcs?.TrySetResult(port);
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing message before UI: {ex.Message}");
            }



            await Dispatcher.Invoke(async () =>
            {
                try
                {
                    var json = JsonDocument.Parse(message);
                    var root = json.RootElement;
                    string toId = root.TryGetProperty("to", out var toProp) ? toProp.GetString() : null;

                    if (root.TryGetProperty("payload", out var payload))
                    {
                        string status = payload.GetProperty("status").GetString();
                        string command = payload.GetProperty("command").GetString();
                        if (command == "login" && status == "success")
                        {
                            // Gán lại tcpClient.Id bằng toId (session id GUID)
                            tcpClient.Id = toId;
                            SessionManager.Instance.ClientId = toId;
                            Console.WriteLine($"[SYNC][LOGIN] Set tcpClient.Id = {toId}");

                            Console.WriteLine("==> LOGIN SUCCESS khi reconnect <==");
                            loginCompletedTcs?.TrySetResult(true);

                            // DỌN DẸP BUFFER TRƯỚC KHI GỬI TIẾP
                            await DrainSocketAsync(tcpClient);
                        }
                        else if (command == "register_room" && status == "success")
                        {
                            // Đăng ký phòng thành công
                            Console.WriteLine("Room registered successfully!");
                            registerRoomCompletedTcs?.TrySetResult(true);
                            // Có thể cập nhật UI hoặc trạng thái tại đây nếu muốn
                        }

                        else if (status == "success" && command == "join_room")
                        {
                            if (payload.TryGetProperty("user", out var userProp) &&
                                payload.TryGetProperty("partner", out var partnerProp))
                            {
                                var user = JsonSerializer.Deserialize<UserInfo>(userProp.GetRawText());
                                var partner = JsonSerializer.Deserialize<UserInfo>(partnerProp.GetRawText());

                                usrEmail.Content = user.email;
                                usrName.Text = user.username;
                                ptnEmail.Content = partner.email;
                                ptnName.Text = partner.username;
                                role = "controller";
                                clientId = user.id;
                                targetId = partner.id;
                            }
                            connected = true;
                            ////////////////////////////////////////// TRAO ĐỔI KEY - TÍNH KEY
                            Console.WriteLine("Computing shared key.....");
                            MyPrivKey = lblYourPass.Text;
                            OtherPublicKey = txtPassword.Password;
                            SharedKey = CryptoService.ComputeSharedKey(MyPrivKey, OtherPublicKey);
                            Console.WriteLine($"Computed shared key: {SharedKey}");

                            //Console.WriteLine($"OtherPublicKey: {OtherPublicKey}");
                            //Console.WriteLine($"MyPrivKey: {MyPrivKey}");
                            ///////////////////////////////////////// TRAO ĐỔI KEY - REMOTE GỬI PUBKEY
                            var sharedPubkey = new
                            {
                                command = "send_pubkey",
                                Pubkey = lblYourPass.Text

                            };
                            await tcpClient.SendMessageAsync(clientId, targetId, sharedPubkey);
                            System.Windows.MessageBox.Show("Join room thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);



                            Remote.Visibility = Visibility.Visible;
                        }
                        else if (command == "send_pubkey" && status == "success")
                        {
                            ////////////////////////////////////////// TRAO ĐỔI KEY - NHẬN PUBKEY VÀ TÍNH
                            if (payload.TryGetProperty("message", out var PubkeyProp))
                            {
                                var Pubkey = PubkeyProp.GetString();
                                OtherPublicKey = Pubkey;
                                Console.WriteLine($"Pubkey{Pubkey}");
                            }
                            MyPrivKey = lblYourPass.Text;
                            SharedKey = CryptoService.ComputeSharedKey(OtherPublicKey, MyPrivKey);
                            Console.WriteLine($"Computed shared key: {SharedKey}");
                            //Console.WriteLine($"MyPrivKey: {MyPrivKey}");
                            //Console.WriteLine($"OtherPublicKey: {OtherPublicKey}");
                        }
                        else if (command == "join_room" && status != "success")
                        {
                            string msg = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"Join room failed: {msg}");
                            System.Windows.MessageBox.Show($"Join room failed: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (command == "register_room" && status != "success")
                        {
                            string msg = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"Room registration failed: {msg}");
                            System.Windows.MessageBox.Show($"Room registration failed: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (command == "want_sync" && status != "success")
                        {
                            string msg = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"Sync failed: {msg}");
                            System.Windows.MessageBox.Show($"Sync failed: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (command == "want_diskDetail" && status != "success")
                        {
                            string msg = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"Disk detail failed: {msg}");
                            System.Windows.MessageBox.Show($"Disk detail failed: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (command == "want_CPUDetail" && status != "success")
                        {
                            string msg = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"CPU detail failed: {msg}");
                            System.Windows.MessageBox.Show($"CPU detail failed: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (command == "want_MemoryDetail" && status != "success")
                        {
                            string msg = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"Memory detail failed: {msg}");
                            System.Windows.MessageBox.Show($"Memory detail failed: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (command == "want_GPUDetail" && status != "success")
                        {
                            string msg = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"GPU detail failed: {msg}");
                            System.Windows.MessageBox.Show($"GPU detail failed: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (command == "want_processList" && status != "success")
                        {
                            string msg = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"Process list failed: {msg}");
                            System.Windows.MessageBox.Show($"Process list failed: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (command == "SentRemoteInfo" && status != "success")
                        {
                            string msg = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"SentRemoteInfo failed: {msg}");
                            System.Windows.MessageBox.Show($"SentRemoteInfo failed: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (command == "SentDetail" && status != "success")
                        {
                            string msg = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"SentDetail failed: {msg}");
                            System.Windows.MessageBox.Show($"SentDetail failed: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (command == "SentprocessList" && status != "success")
                        {
                            string msg = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"SentprocessList failed: {msg}");
                            System.Windows.MessageBox.Show($"SentprocessList failed: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (status == "info" && command == "partner_joined")
                        {
                            if (payload.TryGetProperty("user", out var userProp) &&
                                payload.TryGetProperty("partner", out var partnerProp))
                            {
                                var user = JsonSerializer.Deserialize<UserInfo>(userProp.GetRawText());
                                var partner = JsonSerializer.Deserialize<UserInfo>(partnerProp.GetRawText());

                                // Partner là chính mình, user là người điều khiển
                                usrEmail.Content = user.email;
                                usrName.Text = user.username;
                                ptnEmail.Content = partner.email;
                                ptnName.Text = partner.username;
                                role = "partner";
                                clientId = partner.id;
                                targetId = user.id;
                            }

                            System.Windows.MessageBox.Show("Partner join room thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                            Home_2.Visibility = Visibility.Visible;
                        }
                        else if (command == "start_share")
                        {
                            if (status == "info")
                            {
                                var from_id = payload.GetProperty("targetId").GetString();
                                var sdp = payload.GetProperty("sdp").GetString();
                                var sdpType = payload.GetProperty("sdpType").GetString();


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
                                    Console.WriteLine($"SDP type không hợp lệ: {sdpType}");

                                }
                            }
                            else if (status == "request")
                            {
                                role = "partner";
                                await _shareScreen.StartScreenSharingAsync(targetId);
                            }
                        }
                        else if (command == "stop_share")
                        {
                            if (status == "request")
                            {
                                Console.WriteLine("Received stop share request from controller");
                                await _shareScreen.StopScreenSharingAsync();
                                Dispatcher.Invoke(() =>
                                {
                                    // Dừng hiển thị
                                    imgAgoraVideo.Source = null;
                                });
                            }
                        }
                        else if (command == "ice_candidate" && status == "info")
                        {
                            // Lấy iceCandidate từ payload
                            if (payload.TryGetProperty("iceCandidate", out var iceCandidateProp))
                            {
                                var iceCandidateJson = iceCandidateProp.GetRawText();
                                await _shareScreen.HandleIncomingIceCandidate(iceCandidateJson);
                            }
                            else
                            {
                                Console.WriteLine("Không tìm thấy iceCandidate trong payload!");
                            }
                        }
                        else if (command == "want_sync" && status == "success")
                        {
                            Console.WriteLine("Received sync request from server");
                            System.Windows.MessageBox.Show("Received sync request from server", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                            if (payload.TryGetProperty("message", out var mess))
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
                                await tcpClient.SendMessageAsync(clientId, targetId, Info);
                                Console.WriteLine("Sent remote info to server, then to client (monitor) ", Pair.id);
                            }
                            else
                            {
                                Console.WriteLine("Sync error: id and target id not found!");
                            }
                        }
                        else if (command == "SentRemoteInfo" && status == "success")
                        {

                            if (payload.TryGetProperty("message", out var Remote_info))
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
                            if (payload.TryGetProperty("message", out var mess))
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
                                await tcpClient.SendMessageAsync(clientId, targetId, Info);
                                Console.WriteLine("Sent remote info to server, then to client (monitor) ", Pair.id);
                            }

                            else
                            {
                                Console.WriteLine("Received detail request info error: id and target id not found!");
                            }
                        }
                        else if (command == "SentDetail" && status == "success")
                        {
                            if (payload.TryGetProperty("message", out var DataDetail))
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
                            if (payload.TryGetProperty("message", out var mess))
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
                                await tcpClient.SendMessageAsync(clientId, targetId, Info);
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

                            if (payload.TryGetProperty("message", out var processList))
                            {

                                var processListObj = JsonSerializer.Deserialize<ProcessList>(processList.GetRawText());
                                Console.WriteLine($"Process list getting");
                                timeGetProcessList.SetValue(System.Windows.Controls.Label.ContentProperty, $"Monitor time: {processListObj.RealTime}");

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
                                var Pair = JsonSerializer.Deserialize<PairID>(mess.GetRawText());
                                //var Data = _ProcessSerivce.getProcessList();
                                _viewModelPCdump.ProcessDump(Mess.PID);
                                Console.WriteLine($"Process dump start sending.........");
                                byte[] fileBytes = File.ReadAllBytes("dumpTemp.dmp");
                                byte[] compress_data = CompressService.Compress(fileBytes);
                                string base64Data = Convert.ToBase64String(compress_data);


                                Console.WriteLine($"Process dump data length: {compress_data.Length} bytes");
                                //await tcpClient.SendFileAsync("dumpTemp.dmp");
                                //await tcpClient.SendFileAsync("dumpTemp.dmp", "SentprocessDump", Mess.id);
                                var Info = new
                                {
                                    command = "SentprocessDump",
                                    info = compress_data,

                                    Monitor_id = Pair.id,//theo doi
                                    Remote_id = Pair.target_id// bị theo dõi ( dự liệu theo dõi là của máy này)
                                };
                                string Infojson = JsonSerializer.Serialize(Info);
                                await tcpClient.SendMessageAsync(Infojson);
                                Console.WriteLine("Sent process dump to server, then to client (monitor) ", Pair.id);
                            }
                            else
                            {
                                Console.WriteLine("Received detail info error: id and target id not found!");
                            }
                        }
                        else if (command == "SentprocessDump" && status == "success")
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

                                //await tcpClient.RelayFileAsync(filePath, dumpLength);
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
                                await tcpClient.SendMessageAsync(clientId, targetId, DiskDetail);
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
                                await tcpClient.SendMessageAsync(clientId, targetId, CPUDetail);
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
                                await tcpClient.SendMessageAsync(clientId, targetId, MemoryDetail);
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
                                await tcpClient.SendMessageAsync(clientId, targetId, MemoryDetail);
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
            timeGetProcessList.SetValue(System.Windows.Controls.Label.ContentProperty, $"Monitor time:");
            if (role == "controller")
            {
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

                        await tcpClient.SendMessageAsync(clientId, targetId, SyncRequest);
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
            System.Windows.MessageBox.Show("Feature is in development", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
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
                        await tcpClient.SendMessageAsync(clientId, targetId, SyncRequest);
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

        private async Task<int> GetPartnerServerPortAsync(string targetId)
        {
            partnerPortTcs = new TaskCompletionSource<int>();

            var getPortRequest = new
            {
                command = "get_partner_port",
                target_id = targetId
            };
            await tcpClient.SendMessageAsync(savedTempId, savedTempId, getPortRequest);

            // Chờ đến khi nhận được port từ server (OnServerMessage sẽ set kết quả)
            return await partnerPortTcs.Task;
        }

        private async Task DrainSocketAsync(CClient tcpClient)
        {
            var stream = tcpClient.Stream;
            while (stream != null && stream.DataAvailable)
            {
                byte[] buffer = new byte[1024];
                await stream.ReadAsync(buffer, 0, buffer.Length);
            }
        }
    }

}
