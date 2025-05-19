using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace SERVER_RemoteMonitoring.Services
{
    public class ClientHandler
    {
        private readonly ClientConnectionWS _client;
        private readonly AuthService _authService;
        private readonly SessionManager _sessionManager;
        private readonly RoomManager _roomManager;

        public ClientHandler(ClientConnectionWS clientConnection, AuthService authService, SessionManager sessionManager, RoomManager roomManager)
        {
            _client = clientConnection;
            _authService = authService;
            _sessionManager = sessionManager;
            _roomManager = roomManager;
        }

        public async Task<bool> ProcessAsync()
        {
            try
            {
                bool authenticated = await AuthenticateClientAsync();
                if (!authenticated) return false;

                await _client.ListenForMessageAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing client {_client.Id}: {ex.Message}");
                return false;
            }
            //finally
            //{
            //    await _client.CloseAsync();
            //    Console.WriteLine($"Client {_client.Id} disconnected.");
            //}
        }

        private async Task<bool> AuthenticateClientAsync()
        {
            while (true)
            {
                var message = await _client.ReceiveMessageAsync();
                var jsonMessage = System.Text.Json.JsonSerializer.Deserialize<BaseRequest>(message);

                if (jsonMessage == null)
                {
                    await SendResponseAsync<string>("error", "", "Invalid message format.");
                    continue;
                }

                var command = jsonMessage.command;

                switch (command)
                {
                    case "login":
                        var loginData = System.Text.Json.JsonSerializer.Deserialize<LoginRequest>(message);
                        return await HandleLoginAsync(loginData);

                    case "register":
                        var registerData = System.Text.Json.JsonSerializer.Deserialize<RegisterRequest>(message);
                        await HandleRegisterAsync(registerData);
                        break;

                    default:
                        return await UnknownCommand(command);
                }
            }
        }

        private async Task HandleRegisterAsync(RegisterRequest data)
        {
            if (string.IsNullOrEmpty(data.username) || string.IsNullOrEmpty(data.email) || string.IsNullOrEmpty(data.password))
            {
                await SendResponseAsync<string>("error", "register", "Invalid registration format.");
                return;
            }

            var username = data.username;
            var email = data.email;
            var password = data.password;

            bool success = await _authService.RegisterAsync(username, email, password);

            if (success)
            {
                await SendResponseAsync<string>("success", "register", "User registered successfully.");
            }
            else
            {
                await SendResponseAsync<string>("fail", "register", "User already exists.");
            }
        }

        private async Task<bool> HandleLoginAsync(LoginRequest data)
        {
            if (string.IsNullOrEmpty(data.username) || string.IsNullOrEmpty(data.password)) {
                await SendResponseAsync<string>("error", "login", "Invalid login format.");
                return false;
            }

            var username = data.username;
            var password = data.password;

            bool success = await _authService.LoginAsync(username, password);
            if (success)
            {
                var user = await _authService.GetUserAsync(username);
                if (user == null)
                {
                    await SendResponseAsync<string>("error", "login", "User not found.");
                    return false;
                }

                var session = new UserSession
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    role = user.Role,
                };

                var response = new LoginMessage
                {
                    id = user.Id.ToString(),
                    username = user.Username,
                    email = user.Email,
                    role = user.Role,
                    token = Guid.NewGuid().ToString() // Generate a token for the session
                };

                _sessionManager.AddSession(_client.Id, session);
                await SendResponseAsync<LoginMessage>("success", "login", response);
                return true;
            }
            else
            {
                await SendResponseAsync<string>("fail", "login", "Invalid username or password.");
                return false;
            }
        }

        private async Task<bool> UnknownCommand(string command)
        {
            await SendResponseAsync<string>("error", command, "Unknown command.");
            return false;
        }

        private async Task SendResponseAsync<T>(string status, string command, T message)
        {
            var response = new BaseResponse<T>
            {
                status = status,
                command = command,
                message = message
            };
            var jsonResponse = System.Text.Json.JsonSerializer.Serialize(response);
            await _client.SendMessageAsync(jsonResponse);
        }

        public async Task HandleMessageAsync(string json)
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("command", out var commandProp))
                return;

            string command = commandProp.GetString();
            switch (command)
            {
                case "join_room":
                    {
                        string targetId = root.GetProperty("target_id").GetString();
                        string targetPassword = root.GetProperty("target_password").GetString();

                        // Xác minh target
                        bool targetOk = _roomManager.VerifyClient(targetId, targetPassword);
                        if (!targetOk)
                        {
                            await SendResponseAsync<string>("fail", "join_room", "ID hoặc password không đúng");
                            return;
                        }

                        // Tham gia phòng
                        if (!await _roomManager.JoinRoom(targetId, _client))
                        {
                            await SendResponseAsync<string>("fail", "join_room", "Không thể tham gia phòng");
                            return;
                        }

                        // Lấy thông tin session
                        var targetSession = _roomManager.GetSessionById(targetId);

                        if (targetSession == null)
                        {
                            MessageBox.Show($"❌ Không tìm thấy session cho targetId = {targetId}. Client chưa login.");
                        }

                        var joiningSession = _sessionManager.GetSession(_client.Id); // Người điều khiển

                        // Gửi response cho người điều khiển
                        var joinResponse = new
                        {
                            status = "success",
                            command = "join_room",
                            user = new
                            {
                                username = joiningSession?.username,
                                email = joiningSession?.email
                            },
                            partner = new
                            {
                                username = targetSession?.username,
                                email = targetSession?.email
                            }
                        };
                        await _client.SendMessageAsync(JsonSerializer.Serialize(joinResponse));

                        // Gửi thông báo cho người bị điều khiển
                        var targetClient = _roomManager.GetClientById(targetId);
                        if (targetClient != null)
                        {
                            // Gửi thông báo cho người bị điều khiển
                            var notifyResponse = new
                            {
                                status = "info",
                                command = "partner_joined",
                                user = new
                                {
                                    username = joiningSession?.username,
                                    email = joiningSession?.email
                                },
                                partner = new
                                {
                                    username = targetSession?.username,
                                    email = targetSession?.email
                                }
                            };
                            await targetClient.SendMessageAsync(JsonSerializer.Serialize(notifyResponse));
                        }
                        break;
                    }

                case "register_room":
                    {
                        string id = root.GetProperty("id").GetString();
                        string password = root.GetProperty("password").GetString();

                        var session = _sessionManager.GetSession(_client.Id);
                        await _roomManager.RegisterClient(id, password, _client, session);
                        await SendResponseAsync<string>("success", "register_room", "Room registered.");
                        break;
                    }

                default:
                    await SendResponseAsync<string>("error", command, "Unknown command.");
                    break;
            }

        }

        //private async Task ListenMessageAsync()
        //{
        //    _client.MessageReceived += (msg) =>
        //    {
        //        Console.WriteLine($"Message from client {_client.Id}: {msg}");
        //        // Handle incoming messages here
        //    };

        //    await _client.ListenForMessageAsync();
        //}

        // Define a base message json 
        private class BaseRequest
        {
            public string command { get; set; }
        }

        private class LoginRequest : BaseRequest
        {
            public string username { get; set; }
            public string password { get; set; }
        }

        // Define a class to represent the expected JSON structure
        private class RegisterRequest : BaseRequest
        {
            public string username { get; set; }
            public string email { get; set; }
            public string password { get; set; }
        }

        private class LoginMessage
        {
            public string id { get; set; }
            public string username { get; set; }
            public string email { get; set; }
            public string role { get; set; } // e.g., "Admin", "User"
            public string token { get; set; }
        }

        private class BaseResponse<T>
        {
            public string status { get; set; }
            public string command { get; set; }
            public T message { get; set; }
        }
    }
}
