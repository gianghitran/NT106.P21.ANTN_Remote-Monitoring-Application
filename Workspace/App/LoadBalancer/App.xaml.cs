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
        private static List<ServerInfo> servers = new List<ServerInfo>
        {
            new ServerInfo("127.0.0.1", 8080),
            new ServerInfo("127.0.0.1", 8081),
            new ServerInfo("127.0.0.1", 8082)
        };
        private static TcpListener listener;
        private static int loadBalancerPort = 8001;

        private static Dictionary<string, TcpClient> clientMap = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Console.WriteLine($"Load Balancer is running on port: {loadBalancerPort}");
            StartLoadBalancer();
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
            ServerInfo server = GetServerWithLeastConnections();
            server.IncrementConnection();
            UpdateServerInfo();

            TcpClient serverClient = new TcpClient(server.IP, server.Port);
            NetworkStream clientStream = client.GetStream();
            NetworkStream serverStream = serverClient.GetStream();

            var clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            Console.WriteLine($"[CONNECT] New client {clientEndPoint} connected → {server.IP}:{server.Port}");


            var clientToServer = new Thread(() => ForwardJson(clientStream, serverStream, client).Wait());
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
            servers.Sort((s1, s2) => s1.ConnectionCount.CompareTo(s2.ConnectionCount));
            return servers[0];
        }

        private static async Task ForwardJson(NetworkStream input, NetworkStream output, TcpClient origin)
        {
            byte[] lengthBuffer = new byte[4];
            while (true)
            {
                try
                {
                    // Đọc 4 byte độ dài
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
                        if (bytesRead == 0) break;
                        totalRead += bytesRead;
                    }

                    // Log hoặc xử lý (nếu cần)
                    string json = Encoding.UTF8.GetString(messageBuffer);
                    Console.WriteLine($"Forwarding message: {json}");

                    // Gửi sang đầu kia
                    await output.WriteAsync(lengthBuffer, 0, 4);
                    await output.WriteAsync(messageBuffer, 0, messageLength);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ForwardJson error: {ex.Message}");
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
