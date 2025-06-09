using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Text.Json;
using System.Text;

namespace LoadBalancer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static List<ServerInfo> servers = new List<ServerInfo>();
        private static TcpListener listener;
        private static int loadBalancerPort = 8001;

        private static Dictionary<string, TcpClient> clientMap = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Console.WriteLine($"Load Balancer is running on port: {loadBalancerPort}");
            StartLoadBalancer();
            StartPingServers(); // Thêm dòng này
        }

        private static void StartLoadBalancer()
        {
            listener = new TcpListener(IPAddress.Any, loadBalancerPort);
            listener.Start();
            Thread acceptThread = new Thread(AcceptClients);
            acceptThread.Start();
        }

        private static void AcceptClients()
        {
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();
            }
        }

        private static void HandleClient(TcpClient client)
        {
            NetworkStream clientStream = client.GetStream();

            // Đọc thử 4 byte đầu để lấy độ dài message
            byte[] lengthBuffer = new byte[4];
            int read = clientStream.Read(lengthBuffer, 0, 4);
            if (read == 0)
            {
                client.Close();
                return;
            }
            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
            byte[] messageBuffer = new byte[messageLength];
            int totalRead = 0;
            while (totalRead < messageLength)
            {
                int bytesRead = clientStream.Read(messageBuffer, totalRead, messageLength - totalRead);
                if (bytesRead == 0)
                {
                    client.Close();
                    return;
                }
                totalRead += bytesRead;
            }
            string json = Encoding.UTF8.GetString(messageBuffer);

            // Kiểm tra nếu là gói đăng ký server
            if (json.Contains("\"type\":\"register_server\""))
            {
                try
                {
                    var doc = JsonDocument.Parse(json);
                    string ip = doc.RootElement.GetProperty("ip").GetString();
                    int port = doc.RootElement.GetProperty("port").GetInt32();
                    RegisterServer(ip, port);
                    Console.WriteLine($"[REGISTER] Server {ip}:{port} registered.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[REGISTER FAIL] {ex.Message}");
                }
                client.Close();
                return;
            }

            // Nếu không phải gói đăng ký server, tiếp tục như client thường
            ServerInfo server;
            try
            {
                server = GetServerWithLeastConnections();
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("[REJECT] No backend server available. Closing client connection.");
                client.Close();
                return;
            }
            server.IncrementConnection();
            UpdateServerInfo();

            TcpClient serverClient = new TcpClient(server.IP, server.Port);
            NetworkStream serverStream = serverClient.GetStream();

            var clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            Console.WriteLine($"[CONNECT] New client {clientEndPoint} connected → {server.IP}:{server.Port}");

            Queue<(byte[] len, byte[] msg)> initialQueue = new();
            initialQueue.Enqueue((lengthBuffer, messageBuffer));

            var clientToServer = new Thread(() => ForwardJson(clientStream, serverStream, client, initialQueue).Wait());
            var serverToClient = new Thread(() => ForwardJson(serverStream, clientStream, serverClient).Wait());

            try
            {
                clientToServer.Start();
                serverToClient.Start();

                clientToServer.Join();
                serverToClient.Join();
            }
            finally
            {
                Console.WriteLine($"[DISCONNECT] Client {clientEndPoint} disconnected from {server.IP}:{server.Port}");
                server.DecrementConnection();
                UpdateServerInfo();

                client.Close();
                serverClient.Close();
            }
        }


        private static ServerInfo GetServerWithLeastConnections()
        {
            if (servers.Count == 0)
            {
                Console.WriteLine("No backend server registered! Rejecting client.");
                throw new InvalidOperationException("No backend server registered.");
            }
            servers.Sort((s1, s2) => s1.ConnectionCount.CompareTo(s2.ConnectionCount));
            return servers[0];
        }

        private static async Task ForwardJson(NetworkStream input, NetworkStream output, TcpClient origin, Queue<(byte[] len, byte[] msg)> initialQueue = null)
        {
            byte[] lengthBuffer = new byte[4];

            // 1. Gửi các gói đầu tiên nếu có (từ initialQueue)
            if (initialQueue != null)
            {
                while (initialQueue.Count > 0)
                {
                    var (len, msg) = initialQueue.Dequeue();
                    await output.WriteAsync(len, 0, 4);
                    await output.WriteAsync(msg, 0, msg.Length);

                    string firstJson = Encoding.UTF8.GetString(msg);
                    Console.WriteLine($"[FORWARD-FIRST] {firstJson}");
                }
            }

            // 2. Tiếp tục forward các gói sau
            while (true)
            {
                try
                {
                    int totalRead = await input.ReadAsync(lengthBuffer, 0, 4);
                    if (totalRead == 0)
                    {
                        Console.WriteLine($"[EOF] Stream closed by {origin.Client.RemoteEndPoint}");
                        break;
                    }

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                    byte[] messageBuffer = new byte[messageLength];
                    totalRead = 0;

                    while (totalRead < messageLength)
                    {
                        int bytesRead = await input.ReadAsync(messageBuffer, totalRead, messageLength - totalRead);
                        if (bytesRead == 0)
                        {
                            if (totalRead > 0)
                            {
                                string partialJson = Encoding.UTF8.GetString(messageBuffer, 0, totalRead);
                                Console.WriteLine($"[PARTIAL] Incomplete message from {origin.Client.RemoteEndPoint}: {partialJson}");
                            }
                            break;
                        }
                        totalRead += bytesRead;
                    }

                    if (totalRead == messageLength)
                    {
                        string json = Encoding.UTF8.GetString(messageBuffer);
                        string hex = BitConverter.ToString(messageBuffer, 0, messageLength).Replace("-", " ");
                        Console.WriteLine($"[RAW HEX] {hex}");

                        // Bỏ qua nếu là ping
                        if (json.Contains("\"type\":\"ping\"") || json.Contains("\"command\":\"ping\""))
                        {
                            Console.WriteLine($"[PING] Ignored ping from {origin.Client.RemoteEndPoint}");
                            continue;
                        }

                        // Nếu là đăng ký server
                        if (json.Contains("\"type\":\"register_server\""))
                        {
                            try
                            {
                                var doc = JsonDocument.Parse(json);
                                string ip = doc.RootElement.GetProperty("ip").GetString();
                                int port = doc.RootElement.GetProperty("port").GetInt32();
                                RegisterServer(ip, port);
                                Console.WriteLine($"[REGISTER] Server {ip}:{port} dynamically registered.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[REGISTER FAIL] {ex.Message}");
                            }
                            continue; // không forward gói này
                        }

                        Console.WriteLine($"[FORWARD] {json}");
                        await output.WriteAsync(lengthBuffer, 0, 4);
                        await output.WriteAsync(messageBuffer, 0, messageLength);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] ForwardJson: {ex.Message}");
                    break;
                }
            }
        }



        private static void UpdateServerInfo()
        {
            Console.Clear();
            foreach (var server in servers)
            {
                Console.WriteLine($"Server {server.IP}:{server.Port} - Connections: {server.ConnectionCount}");
            }
        }

        private static void RegisterServer(string ip, int port)
        {
            lock (servers)
            {
                if (!servers.Any(s => s.IP == ip && s.Port == port))
                {
                    servers.Add(new ServerInfo(ip, port));
                    Console.WriteLine($"[REGISTER] Server {ip}:{port} registered.");
                    UpdateServerInfo();
                }
            }
        }

        private static void StartPingServers()
        {
            new Thread(() =>
            {
                while (true)
                {
                    lock (servers)
                    {
                        var toRemove = new List<ServerInfo>();
                        foreach (var server in servers)
                        {
                            try
                            {
                                using (var tcp = new TcpClient())
                                {
                                    var result = tcp.BeginConnect(server.IP, server.Port, null, null);
                                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                                    if (!success || !tcp.Connected)
                                        throw new Exception("Ping timeout");
                                }
                            }
                            catch
                            {
                                Console.WriteLine($"[PING FAIL] Server {server.IP}:{server.Port} unreachable. Removing from list.");
                                toRemove.Add(server);
                            }
                        }
                        foreach (var s in toRemove)
                        {
                            servers.Remove(s);
                        }
                        if (toRemove.Count > 0)
                            UpdateServerInfo();
                    }
                    Thread.Sleep(2000); // Ping mỗi 2 giây
                }
            })
            { IsBackground = true }.Start();
        }

        public class ServerInfo
        {
            public string IP { get; }
            public int Port { get; }
            private int connectionCount;
            public int ConnectionCount => connectionCount;

            public ServerInfo(string ip, int port)
            {
                IP = ip;
                Port = port;
                connectionCount = 0;
            }

            public void IncrementConnection()
            {
                Interlocked.Increment(ref connectionCount);
            }

            public void DecrementConnection()
            {
                Interlocked.Decrement(ref connectionCount);
            }
        }
    }

}
