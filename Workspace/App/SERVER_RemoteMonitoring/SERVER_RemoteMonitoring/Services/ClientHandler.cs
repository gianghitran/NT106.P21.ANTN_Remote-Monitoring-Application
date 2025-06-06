using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using SIPSorcery.Net;
using TinyJson;
using SERVER_RemoteMonitoring.ViewModels;

namespace SERVER_RemoteMonitoring.Services
{
    public class ClientHandler
    {
        private readonly TCPClient _client;
        private readonly AuthService _authService;
        private readonly SessionManager _sessionManager;
        private readonly RoomManager _roomManager;

        public ClientHandler(TCPClient clientConnection, AuthService authService, SessionManager sessionManager, RoomManager roomManager)
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
                Console.WriteLine($"Client {_client.Id} authenticated: {authenticated}");
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
            if (string.IsNullOrEmpty(data.username) || string.IsNullOrEmpty(data.password))
            {
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
            Console.WriteLine($"Received command: {command} from client {_client.Id}");
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
                                id = joiningSession?.tempId, // id tạm thời (chuỗi random)
                                username = joiningSession?.username,
                                email = joiningSession?.email
                            },
                            partner = new
                            {
                                id = targetSession?.tempId, // id tạm thời (chuỗi random)
                                username = targetSession?.username,
                                email = targetSession?.email
                            }
                        };
                        await _client.SendMessageAsync(JsonSerializer.Serialize(joinResponse));

                        // LẤY targetClient trước khi gửi notify
                        var targetClient = _roomManager.GetClientById(targetId);

                        if (targetClient != null)
                        {
                            var notifyResponse = new
                            {
                                status = "info",
                                command = "partner_joined",
                                user = new
                                {
                                    id = joiningSession?.tempId,
                                    username = joiningSession?.username,
                                    email = joiningSession?.email
                                },
                                partner = new
                                {
                                    id = targetSession?.tempId,
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

                case "start_share":
                    {
                        // Lấy targetId, sdp và sdp_type từ client
                        string targetId = root.GetProperty("targetId").GetString();
                        string sdp = root.GetProperty("sdp").GetString();
                        string sdpType = root.GetProperty("sdpType").GetString();

                        // Tìm client đích theo targetId (người share màn hình)
                        var targetClient = _roomManager.GetClientById(targetId);

                        if (targetClient != null)
                        {
                            if (sdpType == "offer")
                            {
                                var offerMessage = new
                                {
                                    command = "start_share",
                                    status = "info",
                                    targetId = _client.Id, // ID của client gửi offer
                                    sdp = sdp,
                                    sdpType = sdpType
                                };

                                // Gửi offer đến client đích
                                await targetClient.SendMessageAsync(JsonSerializer.Serialize(offerMessage));
                            }
                            else if (sdpType == "answer")
                            {
                                // Nếu là answer, gửi lại cho client đã gửi offer
                                var answerMessage = new
                                {
                                    command = "start_share",
                                    status = "info",
                                    targetId = _client.Id, // ID của client muốn gửi answer
                                    sdp = sdp,
                                    sdpType = sdpType
                                };
                                await targetClient.SendMessageAsync(JsonSerializer.Serialize(answerMessage));
                            }
                            else
                            {
                                await SendResponseAsync<string>("error", "start_share", "Invalid SDP type.");

                            }
                        }
                        else
                        {
                            await SendResponseAsync<string>("fail", "start_share", $"Không tìm thấy client có ID = {targetId}");
                        }
                        break;
                    }

                case "ice_candidate":
                    {
                        string targetId = root.GetProperty("targetId").GetString();


                        var targetClient = _roomManager.GetClientById(targetId);

                        RTCIceCandidateInit iceCandidate = root.GetProperty("iceCandidate").Deserialize<RTCIceCandidateInit>();

                        Console.WriteLine(iceCandidate);

                        var candidateMessage = new
                        {
                            command = "ice_candidate",
                            status = "info",
                            iceCandidate = iceCandidate,
                        };

                        var jsonMessage = candidateMessage.ToJson();

                        await targetClient.SendMessageAsync(jsonMessage);

                        break;
                    }
                case "want_sync":
                    {
                        // Lấy targetId client
                        string targetId = root.GetProperty("target_id").GetString();
                        string Id = root.GetProperty("id").GetString();

                        var targetClient = _roomManager.GetClientById(targetId);

                        if (targetClient != null) 
                        { 
                            //var SyncRequest = new
                            //{
                            //    status="success",
                            //    command = "want_sync",
                            //    id = Id,
                            //    target_id = targetId
                            //};
                            var syncMessage = new BaseResponse<object>
                            {
                                status = "success",
                                command = "want_sync",
                                message = new
                                {
                                    id = Id,
                                    target_id = targetId
                                }
                            };
                            await targetClient.SendMessageAsync(JsonSerializer.Serialize(syncMessage));

                        }
                        else
                        {
                            await SendResponseAsync<string>("fail", "want_sync", $"Không tìm thấy client có ID = {targetId}");
                        }
                            break;
                    }
                case "SentRemoteInfo":
                        {
                        string targetId = root.GetProperty("Monitor_id").GetString();//người theo dõi
                        string Id = root.GetProperty("Remote_id").GetString();//bị theo dõi
                        var info = root.GetProperty("info");
                        var infoMemory = root.GetProperty("infoMemory");
                        var infoCPU = root.GetProperty("infoCPU");


                        var infoJson = info.GetRawText();
                        var infoJsonMemory = infoMemory.GetRawText();
                        var infoJsonCPU = infoCPU.GetRawText();

                        //var drives = JsonSerializer.Deserialize<List<DriveDiskModel>>(infoJson);
                        //var memory = JsonSerializer.Deserialize<DriveMemoryModel>(infoJsonMemory);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };

                        // Deserialize từng phần riêng
                        var drives = JsonSerializer.Deserialize<List<DriveDiskModel>>(infoJson, options);
                        var memory = JsonSerializer.Deserialize<List<DriveMemoryModel>>(infoJsonMemory, options);
                        var cpu = JsonSerializer.Deserialize<List<DriveCPUModel>>(infoJsonCPU, options);
                        
                        var remoteInfo = new RemoteInfoMessage
                        {
                            Drives = drives,
                            Memory = memory,
                            CPU = cpu
                        };
                        var targetClient = _roomManager.GetClientById(targetId);
                        if (targetClient != null)
                        {
                            var RemoteData = new BaseResponse_RemoteInfo<RemoteInfoMessage>
                            {
                                status = "success",
                                command = "SentRemoteInfo",
                                message = remoteInfo
                            };

                            await targetClient.SendMessageAsync(JsonSerializer.Serialize(RemoteData));
                        }
                        else
                        {
                            await SendResponseAsync<string>("fail", "SentRemoteInfo", $"Không tìm thấy client có ID = {targetId}");
                        }
                        break;
                        }
                case "want_diskDetail":
                    {
                        string targetId = root.GetProperty("target_id").GetString();
                        string Id = root.GetProperty("id").GetString();

                        var targetClient = _roomManager.GetClientById(targetId);
                        if (targetClient != null)
                        {
                            var Data = new BaseResponse<object>
                            {
                                status = "success",
                                command = "want_diskDetail",
                                message = new 
                                {
                                    id = Id,
                                    target_id = targetId
                                }
                            };

                            await targetClient.SendMessageAsync(JsonSerializer.Serialize(Data));
                        }
                        else
                        {
                            await SendResponseAsync<string>("fail", "want_diskDetail", $"Client not found ID = {targetId}");
                        }
                        break;
                    }
                
                case "want_MemoryDetail":
                    {
                        string targetId = root.GetProperty("target_id").GetString();
                        string Id = root.GetProperty("id").GetString();

                        var targetClient = _roomManager.GetClientById(targetId);
                        if (targetClient != null)
                        {
                            var Data = new BaseResponse<object>
                            {
                                status = "success",
                                command = "want_MemoryDetail",
                                message = new
                                {
                                    id = Id,
                                    target_id = targetId
                                }
                            };

                            await targetClient.SendMessageAsync(JsonSerializer.Serialize(Data));
                        }
                        else
                        {
                            await SendResponseAsync<string>("fail", "want_MemoryDetail", $"Client not found ID = {targetId}");
                        }
                        break;
                    }
                case "want_CPUDetail":
                    {
                        string targetId = root.GetProperty("target_id").GetString();
                        string Id = root.GetProperty("id").GetString();

                        var targetClient = _roomManager.GetClientById(targetId);
                        if (targetClient != null)
                        {
                            var Data = new BaseResponse<object>
                            {
                                status = "success",
                                command = "want_CPUDetail",
                                message = new
                                {
                                    id = Id,
                                    target_id = targetId
                                }
                            };

                            await targetClient.SendMessageAsync(JsonSerializer.Serialize(Data));
                        }
                        else
                        {
                            await SendResponseAsync<string>("fail", "want_CPUDetail", $"Client not found ID = {targetId}");
                        }
                        break;
                    }
                case "want_GPUDetail":
                    {
                        string targetId = root.GetProperty("target_id").GetString();
                        string Id = root.GetProperty("id").GetString();

                        var targetClient = _roomManager.GetClientById(targetId);
                        if (targetClient != null)
                        {
                            var Data = new BaseResponse<object>
                            {
                                status = "success",
                                command = "want_GPUDetail",
                                message = new
                                {
                                    id = Id,
                                    target_id = targetId
                                }
                            };

                            await targetClient.SendMessageAsync(JsonSerializer.Serialize(Data));
                        }
                        else
                        {
                            await SendResponseAsync<string>("fail", "want_GPUDetail", $"Client not found ID = {targetId}");
                        }
                        break;
                    }
                case "SentDetail":
                    {
                        string targetId = root.GetProperty("Monitor_id").GetString();//người theo dõi
                        string Id = root.GetProperty("Remote_id").GetString();//bị theo dõi
                        var info = root.GetProperty("info");
                        
                        string infoJson = info.GetString();

                        var targetClient = _roomManager.GetClientById(targetId);
                        if (targetClient != null)
                        {
                            var DetailData = new 
                            {
                                status = "success",
                                command = "SentDetail",
                                message = infoJson
                            };

                            await targetClient.SendMessageAsync(JsonSerializer.Serialize(DetailData));
                        }
                        else
                        {
                            await SendResponseAsync<string>("fail", "SentRemoteInfo", $"Client not found ID ID = {targetId}");
                        }
                        break;
                    }
                case "want_processList":
                    {
                        string targetId = root.GetProperty("target_id").GetString();
                        string Id = root.GetProperty("id").GetString();

                        var targetClient = _roomManager.GetClientById(targetId);
                        if (targetClient != null)
                        {
                            var Data = new BaseResponse<object>
                            {
                                status = "success",
                                command = "want_processList",
                                message = new
                                {
                                    id = Id,
                                    target_id = targetId
                                }
                            };

                            await targetClient.SendMessageAsync(JsonSerializer.Serialize(Data));
                        }
                        else
                        {
                            await SendResponseAsync<string>("fail", "want_processList", $"Client not found ID = {targetId}");
                        }
                        break;
                    }
                case "SentprocessList":
                    {
                        string targetId = root.GetProperty("Monitor_id").GetString();//người theo dõi
                        string Id = root.GetProperty("Remote_id").GetString();//bị theo dõi
                        var processList = root.GetProperty("info");

                        var targetClient = _roomManager.GetClientById(targetId);
                        if (targetClient != null)
                        {
                            var DetailData = new 
                            {
                                status = "success",
                                command = "SentprocessList",
                                message = processList
                            };

                            await targetClient.SendMessageAsync(JsonSerializer.Serialize(DetailData), "SentprocessList");
                        }
                        else
                        {
                            await SendResponseAsync<string>("fail", "SentprocessList", $"Client not found ID ID = {targetId}");
                        }
                        break;
                    }
                case "want_processDump":
                    {
                        string targetId = root.GetProperty("target_id").GetString();
                        string Id = root.GetProperty("id").GetString();
                        String ProcessID = root.GetProperty("ProcessPID").GetString();
                        var targetClient = _roomManager.GetClientById(targetId);
                        if (targetClient != null)
                        {
                            var WantPID = new BaseResponse<object>
                            {
                                status = "success",
                                command = "want_processDump",
                                message = new
                                {
                                    id = Id,
                                    target_id = targetId,
                                    PID = ProcessID
                                }
                            };

                            await targetClient.SendMessageAsync(JsonSerializer.Serialize(WantPID));
                        }
                        else
                        {
                            await SendResponseAsync<string>("fail", "want_processDump", $"Client not found ID = {targetId}");
                        }
                        break;
                    }
                case "SentprocessDump":
                    {
                        
                        string targetId = root.GetProperty("Monitor_id").GetString();//người theo dõi
                        string Id = root.GetProperty("Remote_id").GetString();//bị theo dõi
                        var processDumpLength  = root.GetProperty("info");

                        var targetClient = _roomManager.GetClientById(targetId);
                        if (targetClient != null)
                        {
                            var DetailData = new
                            {
                                status = "success",
                                command = "SentprocessDump",
                                message = processDumpLength
                            };

                            await targetClient.SendMessageAsync(JsonSerializer.Serialize(DetailData), "SentprocessDump");
                        }
                        else
                        {
                            await SendResponseAsync<string>("fail", "SentprocessList", $"Client not found ID ID = {targetId}");
                        }
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
