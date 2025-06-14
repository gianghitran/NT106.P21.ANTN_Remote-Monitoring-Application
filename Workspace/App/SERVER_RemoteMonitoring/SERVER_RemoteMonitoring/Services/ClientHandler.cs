﻿using System;
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
using SERVER_RemoteMonitoring.Models;
using SERVER_RemoteMonitoring.Data;

namespace SERVER_RemoteMonitoring.Services
{
    public class ClientHandler
    {
        private readonly TCPClient _client;
        private readonly AuthService _authService;
        private readonly SessionManager _sessionManager;
        private readonly RoomManager _roomManager;
        private readonly DatabaseService _dbService;
        private readonly SaveLogService _saveLogService;

        public ClientHandler(
            TCPClient clientConnection,
            AuthService authService,
            SessionManager sessionManager,
            RoomManager roomManager,
            DatabaseService dbService,
            SaveLogService saveLogService)
        {
            _client = clientConnection;
            _authService = authService;
            _sessionManager = sessionManager;
            _roomManager = roomManager;
            _dbService = dbService;
            _saveLogService = saveLogService;
        }

        public async Task<bool> ProcessAsync(byte[] firstMessageBuffer = null, int firstMessageLength = 0)
        {
            try
            {
                bool authenticated;
                if (firstMessageBuffer != null && firstMessageLength > 0)
                {
                    authenticated = await AuthenticateClientAsync(firstMessageBuffer, firstMessageLength);
                }
                else
                {
                    authenticated = await AuthenticateClientAsync();
                }
                Console.WriteLine($"Client {_client.Id} authenticated: {authenticated}");
                return authenticated;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing client {_client.Id}: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> AuthenticateClientAsync(byte[] firstMessageBuffer = null, int firstMessageLength = 0)
        {
            bool first = firstMessageBuffer != null && firstMessageLength > 0;
            while (true)
            {
                string message;
                if (first)
                {
                    message = Encoding.UTF8.GetString(firstMessageBuffer, 0, firstMessageLength);
                    first = false;
                }
                else
                {
                    message = await _client.ReceiveMessageAsync();
                }

                // Parse envelope
                Envelope envelope = null;
                try
                {
                    envelope = JsonSerializer.Deserialize<Envelope>(message);
                }
                catch
                {
                    await SendResponseAsync<string>("error", "", "Invalid message format.");
                    continue;
                }
                if (envelope == null || envelope.payload.ValueKind != JsonValueKind.Object)
                {
                    await SendResponseAsync<string>("error", "", "Invalid envelope format.");
                    continue;
                }

                // Parse payload
                var payloadJson = envelope.payload.GetRawText();
                var baseRequest = JsonSerializer.Deserialize<BaseRequest>(payloadJson);
                if (baseRequest == null)
                {
                    await SendResponseAsync<string>("error", "", "Invalid payload format.");
                    continue;
                }

                var command = baseRequest.command;

                switch (command)
                {
                    case "login":
                        var loginData = JsonSerializer.Deserialize<LoginRequest>(payloadJson);
                        return await HandleLoginAsync(loginData);

                    case "register":
                        var registerData = JsonSerializer.Deserialize<RegisterRequest>(payloadJson);
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
                    token = Guid.NewGuid().ToString()
                };
                _sessionManager.AddSession(_client.Id, session);

                await _saveLogService.UserLoginAsync(user.Username, user.Id.ToString(), _client.Id);
                await _saveLogService.LogAsync(user.Id.ToString(), "", "", $"login - Username = {user.Username} - Session_Id = {_client.Id}");

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

            var envelope = new
            {
                from = "server",
                to = _client.Id,
                payload = response
            };

            var jsonEnvelope = JsonSerializer.Serialize(envelope);
            await _client.SendMessageAsync(jsonEnvelope);
        }

        private async Task SendEnvelopeAsync(object payload, string toClientId = null)
        {
            var envelope = new
            {
                from = "server",
                to = toClientId ?? _client.Id,
                payload = payload
            };
            var json = JsonSerializer.Serialize(envelope);

            if (toClientId == null || toClientId == _client.Id)
            {
                await _client.SendMessageAsync(json);
            }
            else
            {
                var targetClient = _roomManager.GetClientById(toClientId);
                if (targetClient != null)
                {
                    await targetClient.SendMessageAsync(json);
                }
                else
                {
                    Console.WriteLine($"Không tìm thấy client có Id = {toClientId} để gửi message.");
                }
            }
        }

        public async Task HandleMessageAsync(string json)
        {
            Envelope envelope;
            string from = null;
            string to = null;
            JsonElement root;

            try
            {
                envelope = JsonSerializer.Deserialize<Envelope>(json);
                from = envelope.from;
                to = envelope.to;

                if (envelope?.payload.ValueKind != JsonValueKind.Object)
                {
                    Console.WriteLine("❌ Invalid payload");
                    Console.WriteLine($"Payload kind: {envelope?.payload.ValueKind}, Json kind: {JsonValueKind.Object}");
                    return;
                }

                var payloadJson = envelope.payload.GetRawText();
                var payloadDoc = JsonDocument.Parse(payloadJson);
                root = payloadDoc.RootElement;

                if (!root.TryGetProperty("command", out var commandProp))
                {
                    Console.WriteLine("❌ Missing 'command' in payload");
                    return;
                }

                string command = commandProp.GetString();
                Console.WriteLine($"📩 Received command: {command} from client {_client.Id} (from={from}, to={to})");

                switch (command)
                {
                    case "join_room":
                        {
                            string targetPassword = root.GetProperty("target_password").GetString();
                            string targetId = to;

                            bool targetOk = await _roomManager.VerifyClient(targetId, targetPassword);
                            Console.WriteLine($"✅ Verify target {targetId} with password {targetPassword}: {targetOk}");
                            if (!targetOk)
                            {
                                await SendResponseAsync<string>("fail", "join_room", "ID hoặc password không đúng");
                                return;
                            }

                            if (!await _roomManager.JoinRoom(targetId, _client, targetPassword))
                            {
                                await SendResponseAsync<string>("fail", "join_room", "Không thể tham gia phòng");
                                return;
                            }

                            var targetSession = _roomManager.GetSessionById(targetId);
                            if (targetSession == null)
                            {
                                await SendResponseAsync<string>("fail", "join_room", "Target is offline or not connected to this server.");
                                return;
                            }

                            var joiningSession = _sessionManager.GetSession(_client.Id);

                            // Ví dụ với join_room:
                            var joinResponsePayload = new
                            {
                                status = "success",
                                command = "join_room",
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
                            await SendEnvelopeAsync(joinResponsePayload);

                            var targetClient = _roomManager.GetClientById(targetId);
                            if (targetClient != null)
                            {
                                var notifyResponsePayload = new
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
                                await SendEnvelopeAsync(notifyResponsePayload, targetClient.Id);
                            }
                            await _saveLogService.LogAsync(_client.Id, "Controller", targetId, "Join room");
                            await _saveLogService.ConnecionAsync(joiningSession?.username, joiningSession?.tempId, "Controller", targetSession?.username, targetSession?.tempId);
                            await _saveLogService.ConnecionAsync(targetSession?.username, targetSession?.tempId, "Remote", joiningSession?.username, joiningSession?.tempId);

                            break;
                        }
                    case "send_pubkey":
                        {
                            string targetId = to;

                            var send_pubkey = root.GetProperty("Pubkey");
                            string pubkey = send_pubkey.ToString();

                            var targetClient = _roomManager.GetClientById(targetId);
                            if (targetClient != null)
                            {
                                var Datapubkey = new
                                {
                                    status = "success",
                                    command = "send_pubkey",
                                    message = pubkey
                                };
                                Console.WriteLine($"message {pubkey}");
                                _saveLogService.LogAsync(_client.Id, "Controller", targetId, "Send public key to compute share key");
                                await SendEnvelopeAsync(Datapubkey, targetClient.Id);
                            }
                            break;
                        }
                    case "start_share":
                        {
                            string targetId = root.GetProperty("targetId").GetString();
                            var targetClient = _roomManager.GetClientById(targetId);

                            try
                            {
                                string type = root.GetProperty("type").GetString();
                                if (type == "offer")
                                {
                                    var offerShareMessage = new
                                    {
                                        command = "start_share",
                                        status = "request",
                                        targetId = _client.Id, // ID của client gửi offer
                                    };
                                    await _saveLogService.LogAsync(_client.Id, "Controller", targetClient.Id, "Offer share screen");
                                    await SendEnvelopeAsync(offerShareMessage, targetClient.Id);
                                }
                            }
                            catch
                            { }

                            // Lấy targetId, sdp và sdp_type từ client
                            string sdp = root.GetProperty("sdp").GetString();
                            string sdpType = root.GetProperty("sdpType").GetString();

                            // Tìm client đích theo targetId (người share màn hình)


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

                                    await _saveLogService.LogAsync(_client.Id, "Controller", targetClient.Id, "Want received share screen");
                                    await SendEnvelopeAsync(offerMessage, targetClient.Id);
                                }
                                else if (sdpType == "answer")
                                {
                                    var answerMessage = new
                                    {
                                        command = "start_share",
                                        status = "info",
                                        targetId = _client.Id, // ID của client gửi answer
                                        sdp = sdp,
                                        sdpType = sdpType
                                    };

                                    await _saveLogService.LogAsync(_client.Id, "Remote", targetClient.Id, "Send share screen");
                                    await SendEnvelopeAsync(answerMessage, targetClient.Id);
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

                    case "stop_share":
                        {
                            string targetId = root.GetProperty("targetId").GetString();
                            var targetClient = _roomManager.GetClientById(targetId);
                            if (targetClient != null)
                            {
                                var stopShareMessage = new
                                {
                                    command = "stop_share",
                                    status = "request",
                                    targetId = _client.Id // ID của client gửi yêu cầu dừng share
                                };
                                await _saveLogService.LogAsync(_client.Id, "Controller", targetClient.Id, "Stop share screen");
                                await SendEnvelopeAsync(stopShareMessage, targetClient.Id);
                            }
                            else
                            {
                                await SendResponseAsync<string>("fail", "stop_share", $"Không tìm thấy client có ID = {targetId}");
                            }
                            break;
                        }

                    case "register_room":
                        {
                            Console.WriteLine($"📥 Received register_room from client {_client.Id}");

                            string id = root.GetProperty("id").GetString();
                            string password = root.GetProperty("password").GetString();
                            var session = _sessionManager.GetSession(_client.Id);

                            if (session != null)
                            {
                                session.tempId = id;
                            }
                            await _roomManager.RegisterClient(id, password, _client, session, _client.ServerPort);
                            _saveLogService.LogAsync(_client.Id, "Controller", "", "register_room");
                            await SendResponseAsync<string>("success", "register_room", "Room registered successfully!");
                            break;
                        }

                    case "ice_candidate":
                        {
                            string targetId = root.GetProperty("targetId").GetString();


                            var targetClient = _roomManager.GetClientById(targetId);

                            RTCIceCandidateInit iceCandidate = root.GetProperty("iceCandidate").Deserialize<RTCIceCandidateInit>();

                            if (iceCandidate.sdpMid == null)
                            {
                                return;
                            }

                            var candidateMessage = new
                            {
                                command = "ice_candidate",
                                status = "info",
                                iceCandidate = iceCandidate
                            };
                            await SendEnvelopeAsync(candidateMessage, targetClient.Id);
                            break;
                        }


                    case "want_sync":
                    case "want_diskDetail":
                    case "want_MemoryDetail":
                    case "want_CPUDetail":
                    case "want_GPUDetail":
                    case "want_processList":
                        {
                            string targetId = to;
                            string id = from;

                            var targetClient = _roomManager.GetClientById(targetId);
                            if (targetClient != null)
                            {
                                var Data = new BaseResponse<object>
                                {
                                    status = "success",
                                    command = command,
                                    message = new
                                    {
                                        id,
                                        target_id = targetId
                                    }
                                };
                                _saveLogService.LogAsync(_client.Id, "Controller", targetId, $"Request: {command}.");

                                await SendEnvelopeAsync(Data, targetClient.Id);
                            }
                            else
                            {
                                await SendResponseAsync<string>("fail", command, $"Không tìm thấy client có ID = {targetId}");
                            }
                            break;
                        }

                    case "SentRemoteInfo":
                        {
                            string targetId = to;
                            var info = root.GetProperty("info");
                            var infoMemory = root.GetProperty("infoMemory");
                            var infoCPU = root.GetProperty("infoCPU");

                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                            var drives = JsonSerializer.Deserialize<List<DriveDiskModel>>(info.GetRawText(), options);
                            var memory = JsonSerializer.Deserialize<List<DriveMemoryModel>>(infoMemory.GetRawText(), options);
                            var cpu = JsonSerializer.Deserialize<List<DriveCPUModel>>(infoCPU.GetRawText(), options);

                            var remoteInfo = new RemoteInfoMessage
                            {
                                Drives = drives,
                                Memory = memory,
                                CPU = cpu
                            };

                            var targetClient = _roomManager.GetClientById(targetId);
                            if (targetClient != null)
                            {
                                var remoteData = new BaseResponse_RemoteInfo<RemoteInfoMessage>
                                {
                                    status = "success",
                                    command = "SentRemoteInfo",
                                    message = remoteInfo
                                };
                                _saveLogService.LogAsync(_client.Id, "Remote", targetId, "Sent Remote Information.");
                                await SendEnvelopeAsync(remoteData, targetClient.Id);
                            }
                            else
                            {
                                await SendResponseAsync<string>("fail", "SentRemoteInfo", $"Không tìm thấy client có ID = {targetId}");
                            }
                            break;
                        }

                    case "SentDetail":
                    case "SentprocessList":
                        {
                            string targetId = to;
                            var info = root.GetProperty("info");

                            var targetClient = _roomManager.GetClientById(targetId);
                            if (targetClient != null)
                            {
                                var response = new
                                {
                                    status = "success",
                                    command = command,
                                    message = info
                                };
                                _saveLogService.LogAsync(_client.Id, "Remote", targetId, $"Sent: {command} Information.");
                                await SendEnvelopeAsync(response, targetClient.Id);
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
                            String Path = root.GetProperty("savepath").GetString();

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
                                        PID = ProcessID,
                                        savepath = Path
                                    }
                                };

                                _saveLogService.LogAsync(_client.Id, "Controller", targetId, $"Sent: {command} Information.");

                                await SendEnvelopeAsync(WantPID, targetClient.Id);
                            }
                            else
                            {
                                await SendResponseAsync<string>("fail", "want_processDump", $"Client not found ID = {targetId}");
                            }
                            break;
                        }
                    case "SentprocessDumpInfo":
                        {

                            string targetId = root.GetProperty("Monitor_id").GetString();//người theo dõi
                            string Id = root.GetProperty("Remote_id").GetString();//bị theo dõi
                            var processDumpIn4 = root.GetProperty("info");

                            var targetClient = _roomManager.GetClientById(targetId);
                            if (targetClient != null)
                            {
                                var DetailData = new
                                {
                                    status = "success",
                                    command = "SentprocessDumpInfo",
                                    message = processDumpIn4
                                };
                                _saveLogService.LogAsync(_client.Id, "Remote", targetId, $"Sent: {command} Information.");

                                await SendEnvelopeAsync(DetailData, targetClient.Id);
                            }
                            else
                            {
                                await SendResponseAsync<string>("fail", "SentprocessDumpInfo", $"Client not found ID ID = {targetId}");
                            }
                            break;
                        }
                    case "get_partner_port":
                        {
                            string targetId = root.GetProperty("target_id").GetString();
                            var db = _dbService.GetDataBaseConnection();
                            var room = await db.Table<RoomClient>().Where(r => r.Id == targetId).FirstOrDefaultAsync();
                            if (room != null)
                            {
                                var responsePayload = new
                                {
                                    status = "success",
                                    command = "get_partner_port",
                                    port = room.ServerPort
                                };
                                await SendEnvelopeAsync(responsePayload);
                            }
                            else
                            {
                                var response = new
                                {
                                    status = "fail",
                                    command = "get_partner_port",
                                    message = "No partner found!"
                                };
                                await SendEnvelopeAsync(response);
                            }
                            break;
                        }
                    case "get_my_port":
                        {
                            var db = _dbService.GetDataBaseConnection();
                            // Lấy tempId của client hiện tại
                            var session = _sessionManager.GetSession(_client.Id);
                            string tempId = session?.tempId;
                            if (!string.IsNullOrEmpty(tempId))
                            {
                                var room = await db.Table<RoomClient>().Where(r => r.Id == tempId).FirstOrDefaultAsync();
                                if (room != null)
                                {
                                    var responsePayload = new
                                    {
                                        status = "success",
                                        command = "get_my_port",
                                        port = room.ServerPort
                                    };
                                    await SendEnvelopeAsync(responsePayload);
                                }
                                else
                                {
                                    var response = new
                                    {
                                        status = "fail",
                                        command = "get_my_port",
                                        message = "Your room not found!"
                                    };
                                    await SendEnvelopeAsync(response);
                                }
                            }
                            else
                            {
                                var response = new
                                {
                                    status = "fail",
                                    command = "get_my_port",
                                    message = "Session not found!"
                                };
                                await SendEnvelopeAsync(response);
                            }
                            break;
                        }
                    case "leave_room":
                        {
                            string id = from;
                            string targetId = to;

                            // Lưu lại thông tin partner trước khi xóa
                            var targetClient = _roomManager.GetClientById(targetId);

                            // Gửi thông báo cho partner (nếu cần)
                            if (targetClient != null)
                            {
                                var notify = new
                                {
                                    status = "info",
                                    command = "partner_left",
                                    message = $"{id} has left the room."
                                };
                                await SendEnvelopeAsync(notify, targetClient.Id);
                            }

                            // Gửi xác nhận cho client vừa rời phòng
                            var response = new
                            {
                                status = "success",
                                command = "leave_room",
                                message = "You have left the room."
                            };
                            await SendEnvelopeAsync(response);

                            // Sau khi đã gửi thông báo, mới xóa mapping phòng ở RoomManager
                            _roomManager.RemoveClient(id);

                            Console.WriteLine($"[ROOM] Client {id} left room with {targetId}");

                            break;
                        }
                    default:
                        {
                            await SendResponseAsync<string>("error", command, "Unknown command.");
                            break;
                        }
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error parsing envelope: {ex.Message}");
            }
        }

        private class BaseRequest
        {
            public string command { get; set; }
        }

        private class LoginRequest : BaseRequest
        {
            public string username { get; set; }
            public string password { get; set; }
        }

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
            public string role { get; set; }
            public string token { get; set; }
        }

        private class BaseResponse<T>
        {
            public string status { get; set; }
            public string command { get; set; }
            public T message { get; set; }
        }

        private class Envelope
        {
            public string from { get; set; }
            public string to { get; set; }
            public JsonElement payload { get; set; }
        }
    } // <-- Đóng class ClientHandler
}
