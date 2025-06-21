using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Net;
using System.Threading;
using System.Windows;
using System.Net.Sockets;
using WebSocketSharp.Net;
using SERVER_RemoteMonitoring.Data;
using System.Text.Json;

namespace SERVER_RemoteMonitoring.Services
{
    public class TCPServer
    {
        private TcpListener _tcpListener;
        private int _port;
        private readonly AuthService _authservice;
        private readonly SaveLogService _saveLogService;
        private readonly SessionManager _sessionManager;
        private readonly List<TCPClient> _clients = new List<TCPClient>();
        private const string uri = "http://localhost:8080/";

        private readonly RoomManager _roomManager;
        private readonly DatabaseService _dbService;

        // Đã hợp nhất các tham số khởi tạo
        public TCPServer(AuthService authService, SaveLogService saveLogService, int port, DatabaseService dbService)
        {
            _authservice = authService;
            _saveLogService = saveLogService;
            _sessionManager = new SessionManager();
            _port = port;
            _tcpListener = new TcpListener(IPAddress.Any, _port);
            _roomManager = new RoomManager(dbService);
            _dbService = dbService;
        }

        public async void Start()
        {
            _tcpListener.Start();
            await RegisterWithLoadBalancer();

            Console.WriteLine("TCP server started at " + _port);
            Task.Run(AcceptClientsAsync);
        }

        private async Task RegisterWithLoadBalancer()
        {
            try
            {
                using TcpClient client = new TcpClient("05fjdolnt.localto.net", 9159); // IP và port của Load Balancer
                using NetworkStream stream = client.GetStream();

                var registerPayload = new
                {
                    type = "register_server",
                    ip = "05fjdolnt.localto.net",
                    port = _port
                };

                string json = JsonSerializer.Serialize(registerPayload);
                byte[] messageBytes = Encoding.UTF8.GetBytes(json);
                byte[] lengthBytes = BitConverter.GetBytes(messageBytes.Length);

                await stream.WriteAsync(lengthBytes, 0, 4);
                await stream.WriteAsync(messageBytes, 0, messageBytes.Length);

                Console.WriteLine($"Registered with LoadBalancer: {_port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to register with LoadBalancer: {ex.Message}");
            }
        }


        public async Task AcceptClientsAsync()
        {
            while (true)
            {
                TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();
                NetworkStream stream = tcpClient.GetStream();

                // Đọc trước 4 byte đầu
                byte[] lengthBuffer = new byte[4];
                int read = 0;
                try
                {
                    read = await stream.ReadAsync(lengthBuffer, 0, 4);
                }
                catch
                {
                    tcpClient.Close();
                    continue;
                }

                if (read < 4)
                {
                    tcpClient.Close();
                    continue;
                }

                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                byte[] messageBuffer = new byte[messageLength];
                int totalRead = 0;
                while (totalRead < messageLength)
                {
                    int bytesRead = await stream.ReadAsync(messageBuffer, totalRead, messageLength - totalRead);
                    if (bytesRead == 0)
                    {
                        tcpClient.Close();
                        continue;
                    }
                    totalRead += bytesRead;
                }

                string clientIp = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();

                // Tạo client và gán gói đầu tiên
                var client = new TCPClient(tcpClient, _roomManager, _port)
                {
                    IP = clientIp,
                    InitialLengthBuffer = lengthBuffer,
                    InitialMessageBuffer = messageBuffer
                };

                _clients.Add(client);
                Console.WriteLine("Client connected: " + client.Id + " from IP: " + clientIp);

                // Bắt đầu xử lý client
                _ = Task.Run(() => HandleClient(client));
            }
        }


        private async Task HandleClient(TCPClient client)
        {
            bool authenticated = false;

            try
            {
                var handler = new ClientHandler(client, _authservice, _sessionManager, _roomManager, _dbService, _saveLogService);
                client.Handler = handler;

                // Truyền message đầu tiên vào ProcessAsync
                authenticated = await handler.ProcessAsync(
                    client.InitialMessageBuffer,
                    client.InitialMessageBuffer != null ? client.InitialMessageBuffer.Length : 0
                );

                if (authenticated)
                {
                    Console.WriteLine("Client authenticated: " + client.Id);

                    var session = _sessionManager.GetSession(client.Id);
                    client.Session = session;

                    await client.ListenForMessageAsync(); // Lắng nghe đến khi client đóng
                }
                else
                {
                    Console.WriteLine("Client authentication failed: " + client.Id);

                    if (client._tcpClient.Connected)
                    {
                        var errorJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            status = "fail",
                            command = "auth",
                            message = "Authentication failed."
                        });
                        await client.SendMessageAsync(errorJson);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client {client.Id}: {ex.Message}");
            }
            finally
            {
                if (!client._tcpClient.Connected)
                {
                    _sessionManager.RemoveSession(client.Id);
                    _clients.Remove(client);
                    Console.WriteLine("Client disconnected: " + client.Id);
                }
            }
        }

        public async void Stop()
        {
            foreach (var client in _clients)
            {
                await client.CloseAsync();
            }
            _tcpListener.Stop();
            Console.WriteLine("WebSocket server stopped");
        }
    }
}
