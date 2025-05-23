using Agora.Rtc;
using RemoteMonitoringApplication.Services;
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

        private WebSocketClient webSocketClient;
        private AgoraManager agoraManager = new AgoraManager();
        private VideoFrameObserver _videoObserver;

        private string role;
        private string targetId;

        public Client()
        {
            InitializeComponent();

            webSocketClient = SessionManager.Instance.WebSocketClient;

            if (webSocketClient == null)
            {
                MessageBox.Show("⚠️ WebSocketClient chưa được khởi tạo!", "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            clientId = ClientIdentity.GenerateRandomId();
            clientPassword = ClientIdentity.GenerateRandomPassword();

            lblYourID.Text = $"{clientId}";
            lblYourPass.Text = $"{clientPassword}";

            webSocketClient.MessageReceived += OnServerMessage;
            this.Loaded += Client_Loaded;

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

            agoraManager.Initialize(
                onRemoteUserJoined: (uid) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"🎉 Remote user joined! UID = {uid}");
                    });
                },
                onRemoteUserLeft: (uid) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"👋 Remote user left! UID = {uid}");
                    });
                }
            );

            // Đăng ký observer
            _videoObserver = new VideoFrameObserver(OnRenderVideoFrame);
            agoraManager.RegisterVideoFrameObserver(_videoObserver);
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
            await webSocketClient.SendMessageAsync(registerJson);
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

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            string targetId = txtUser.Text.Trim();
            string targetPassword = txtPassword.Password;

            var joinRoomRequest = new
            {
                command = "join_room",
                my_id = clientId,
                my_password = clientPassword,
                target_id = targetId,
                target_password = targetPassword
            };

            string json = System.Text.Json.JsonSerializer.Serialize(joinRoomRequest);
            await webSocketClient.SendMessageAsync(json);
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
            MessageBox.Show($"Role hiện tại: {role ?? "(null)"}");

            if (role == "controller")
            {
                MessageBox.Show($"Join channel để XEM màn hình {targetId}");
                await agoraManager.JoinChannel(targetId, isScreenSharer: false);
                _videoObserver = new VideoFrameObserver(OnRenderVideoFrame);
                agoraManager.RegisterVideoFrameObserver(_videoObserver);

                var startShareRequest = new
                {
                    command = "start_share",
                    target_id = targetId
                };
                string json = JsonSerializer.Serialize(startShareRequest);
                await webSocketClient.SendMessageAsync(json);
            }
            else if (role == "partner") { }
            else
            {
                MessageBox.Show("❌ Vai trò chưa được gán! Không thể thực hiện Play!");
            }
        }


        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            agoraManager.StopScreenShare();
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
                        Home_2.Visibility = Visibility.Visible;
                    }
                    else if (command == "start_share" && status == "trigger")
                    {
                        role = "partner";
                        MessageBox.Show($"Đã nhận lệnh chia sẻ");
                        // Join channel với isScreenSharer = true
                        await agoraManager.JoinChannel(targetId, true);
                        // Sau khi join thành công, gọi:
                        await Task.Delay(1500);
                        agoraManager.StartScreenShare();
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
            Clipboard.SetText(lblYourID.Text);
        }

        private void copyIconPass_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Clipboard.SetText(lblYourPass.Text);
        }

        public class UserInfo
        {
            public string id { get; set; }
            public string username { get; set; }
            public string email { get; set; }
        }

        private void OnRenderVideoFrame(byte[] buffer, int width, int height)
        {
            var bitmap = BitmapSource.Create(
                width, height, 96, 96, PixelFormats.Bgra32, null, buffer, width * 4);

            imgAgoraVideo.Dispatcher.Invoke(() =>
            {
                imgAgoraVideo.Source = bitmap;
            });
        }


    }

    public class VideoFrameObserver : IVideoFrameObserver
    {
        private readonly Action<byte[], int, int> _onFrame;

        public VideoFrameObserver(Action<byte[], int, int> onFrame)
        {
            _onFrame = onFrame;
        }

        private byte[] ConvertYUV420ToBGR32(byte[] yBuffer, byte[] uBuffer, byte[] vBuffer, int width, int height)
        {
            int frameSize = width * height;
            byte[] rgbBuffer = new byte[width * height * 4];

            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    int yIndex = j * width + i;
                    int uvIndex = (j / 2) * (width / 2) + (i / 2);

                    int Y = yBuffer[yIndex] & 0xFF;
                    int U = uBuffer[uvIndex] & 0xFF;
                    int V = vBuffer[uvIndex] & 0xFF;

                    int C = Y - 16;
                    int D = U - 128;
                    int E = V - 128;

                    int R = (298 * C + 409 * E + 128) >> 8;
                    int G = (298 * C - 100 * D - 208 * E + 128) >> 8;
                    int B = (298 * C + 516 * D + 128) >> 8;

                    R = Math.Clamp(R, 0, 255);
                    G = Math.Clamp(G, 0, 255);
                    B = Math.Clamp(B, 0, 255);

                    int index = yIndex * 4;
                    rgbBuffer[index] = (byte)B;
                    rgbBuffer[index + 1] = (byte)G;
                    rgbBuffer[index + 2] = (byte)R;
                    rgbBuffer[index + 3] = 255;
                }
            }

            return rgbBuffer;
        }


        public override bool OnRenderVideoFrame(string channelId, uint uid, VideoFrame videoFrame)
        {
            MessageBox.Show($"[Agora] Received frame - channel: {channelId}, uid: {uid}, width: {videoFrame.width}, height: {videoFrame.height}");

            var rgbBuffer = ConvertYUV420ToBGR32(
                videoFrame.yBuffer, videoFrame.uBuffer, videoFrame.vBuffer,
                videoFrame.width, videoFrame.height);

            _onFrame?.Invoke(rgbBuffer, videoFrame.width, videoFrame.height);
            return true;
        }


    }
}
