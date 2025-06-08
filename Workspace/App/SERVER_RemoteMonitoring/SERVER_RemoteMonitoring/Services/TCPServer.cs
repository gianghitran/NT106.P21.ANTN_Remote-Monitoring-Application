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

        public void Start()
        {
            _tcpListener.Start();
            Console.WriteLine("TCP server started at " + _port);
            Task.Run(AcceptClientsAsync);
        }

        public async Task AcceptClientsAsync()
        {
            while (true)
            {
                TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();

                string clientIp = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
                var client = new TCPClient(tcpClient, _roomManager, _port)
                {
                    IP = clientIp
                };

                _clients.Add(client);

                Console.WriteLine("Client connected: " + client.Id + " from IP: " + clientIp);

                // Bắt đầu xử lý client trong một task riêng
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

                while (!authenticated)
                {
                    if (!client._tcpClient.Connected)
                    {
                        Console.WriteLine("Client disconnected before authentication: " + client.Id);
                        return;
                    }

                    authenticated = await handler.ProcessAsync();

                    if (authenticated)
                    {
                        Console.WriteLine("Client authenticated: " + client.Id);

                        var session = _sessionManager.GetSession(client.Id);
                        client.Session = session;

                        await client.SendMessageAsync("Welcome to the WebSocket server!");
                        await client.ListenForMessageAsync(); // Lắng nghe đến khi client đóng
                    }
                    else
                    {
                        Console.WriteLine("Client authentication failed: " + client.Id);

                        // Nếu socket vẫn mở, gửi phản hồi
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client {client.Id}: {ex.Message}");
            }
            finally
            {
                // Chỉ cleanup nếu client đã thật sự ngắt kết nối hoặc gặp lỗi
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
