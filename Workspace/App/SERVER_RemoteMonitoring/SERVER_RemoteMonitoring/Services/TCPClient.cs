using Org.BouncyCastle.Math.Field;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WebSocketSharp;

namespace SERVER_RemoteMonitoring.Services
{
    // Ban dau ClientConnectionWS, gio chuyen thanh TCPClients
    public class TCPClient
    {
        //public WebSocket _webSocket { get; private set; }
        public TcpClient _tcpClient { get; private set; } // TCP client connection
        public string Id { get; private set; }
        public UserSession Session { get; set; } // User session associated with this connection

        public event Action<string> MessageReceived;

        public string IP { get; set; } // Client IP address

        public ClientHandler Handler { get; set; }

        public NetworkStream stream;


        //public TCPClients(WebSocket webSocket, string IP) 
        //{
        //    _webSocket = webSocket;
        //    Id = Guid.NewGuid().ToString(); // Unique per client
        //}

        private readonly RoomManager _roomManager;

        public int ServerPort { get; private set; } // Thêm dòng này
        public byte[]? InitialLengthBuffer { get; set; }
        public byte[]? InitialMessageBuffer { get; set; }

        public TCPClient(TcpClient client, RoomManager roomManager, int serverPort)
        {
            _tcpClient = client;
            stream = _tcpClient.GetStream();
            Id = Guid.NewGuid().ToString();
            _roomManager = roomManager;
            ServerPort = serverPort; // Gán giá trị khi khởi tạo
        }


        //public async Task ListenForMessageAsync()
        //{
        //    var buffer = new byte[1024 * 4];
        //    while (_webSocket.State == WebSocketState.Open)
        //    {
        //        var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        //        //Console.WriteLine("XXXXXX Received message from client " + Id + ": " + result.MessageType.ToString());
        //        if (result.MessageType == WebSocketMessageType.Text)
        //        {
        //            var jsonMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
        //            Console.WriteLine($"Received message from client {Id}: {jsonMessage}");
        //            if (Handler != null)
        //                Console.WriteLine("Handler");
        //                await Handler.HandleMessageAsync(jsonMessage);


        //            //var message = System.Text.Json.JsonSerializer.Deserialize<string>(jsonMessage);

        //            //await HandleClientCommandAsync(message);

        //            //MessageReceived?.Invoke(message);
        //        }
        //        else if (result.MessageType == WebSocketMessageType.Close)
        //        {
        //            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        //        }
        //    }
        //}

        // Listen for messages from the TCP client (loop)
        public async Task ListenForMessageAsync()
        {
            try
            {
                // Xử lý gói đầu tiên nếu có
                if (InitialMessageBuffer != null)
                {
                    var jsonMessage = Encoding.UTF8.GetString(InitialMessageBuffer, 0, InitialMessageBuffer.Length).Trim();
                    Console.WriteLine($"[RECEIVED-FIRST] {jsonMessage}");

                    if (Handler != null)
                    {
                        await Handler.HandleMessageAsync(jsonMessage);
                    }

                    // Reset lại để không xử lý lần 2
                    InitialMessageBuffer = null;
                    InitialLengthBuffer = null;
                }

                while (_tcpClient.Connected)
                {
                    // Read 4 byte độ dài
                    byte[] lengthBuffer = new byte[4];
                    int totalRead = 0;
                    while (totalRead < 4)
                    {
                        int bytesRead = await stream.ReadAsync(lengthBuffer, totalRead, 4 - totalRead);
                        if (bytesRead == 0)
                        {
                            Console.WriteLine("Connection closed by client.");
                            return;
                        }
                        totalRead += bytesRead;
                    }

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                    byte[] messageBuffer = new byte[messageLength];
                    totalRead = 0;

                    while (totalRead < messageLength)
                    {
                        int bytesRead = await stream.ReadAsync(messageBuffer, totalRead, messageLength - totalRead);
                        if (bytesRead == 0)
                        {
                            Console.WriteLine("Connection closed by client.");
                            return;
                        }
                        totalRead += bytesRead;
                    }

                    var jsonMessage = Encoding.UTF8.GetString(messageBuffer, 0, messageLength).Trim();
                    Console.WriteLine($"Received message from client {Id}: {jsonMessage}");

                    if (Handler != null)
                    {
                        await Handler.HandleMessageAsync(jsonMessage);
                    }
                    else
                    {
                        Console.WriteLine("Handler is null");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while listening for messages: {ex.Message}");
            }
        }

        //public async Task ListenForMessageAsync()
        //{
        //    try
        //    {
        //        byte[] buffer = new byte[4096];

        //        while (_tcpClient.Connected)
        //        {
        //            // Đọc thử 4 byte đầu
        //            byte[] lengthCheck = new byte[4];
        //            int readBytes = await stream.ReadAsync(lengthCheck, 0, 4);

        //            if (readBytes == 0)
        //            {
        //                Console.WriteLine("Connection closed by client.");
        //                return;
        //            }

        //            // Kiểm tra xem có phải header length không
        //            if (readBytes == 4)
        //            {
        //                int headerLength = BitConverter.ToInt32(lengthCheck, 0);

        //                if (headerLength > 0 && headerLength < 100000) // đoạn này bạn set max size hợp lý
        //                {
        //                    // Đọc header JSON
        //                    byte[] headerBuffer = new byte[headerLength];
        //                    int totalRead = 0;

        //                    while (totalRead < headerLength)
        //                    {
        //                        int bytesRead = await stream.ReadAsync(headerBuffer, totalRead, headerLength - totalRead);
        //                        if (bytesRead == 0)
        //                        {
        //                            Console.WriteLine("Connection closed by client.");
        //                            return;
        //                        }
        //                        totalRead += bytesRead;
        //                    }

        //                    string headerJson = Encoding.UTF8.GetString(headerBuffer, 0, headerLength).Trim();
        //                    Console.WriteLine($"Received (headered) message from client {Id}: {headerJson}");

        //                    if (Handler != null)
        //                        await Handler.HandleMessageAsync(headerJson);
        //                    else
        //                        Console.WriteLine("Handler is null");
        //                }
        //                else
        //                {
        //                    // Không phải header length hợp lệ → fallback về đọc dạng string JSON cũ
        //                    using (var ms = new MemoryStream())
        //                    {
        //                        // Ghi lại 4 byte vừa đọc
        //                        ms.Write(lengthCheck, 0, readBytes);

        //                        int bytesRead;
        //                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        //                        {
        //                            ms.Write(buffer, 0, bytesRead);

        //                            // Optional: break nếu gặp ký tự kết thúc message (nếu biết)
        //                        }

        //                        string jsonMessage = Encoding.UTF8.GetString(ms.ToArray()).Trim();
        //                        Console.WriteLine($"Received (legacy) message from client {Id}: {jsonMessage}");

        //                        if (Handler != null)
        //                            await Handler.HandleMessageAsync(jsonMessage);
        //                        else
        //                            Console.WriteLine("Handler is null");
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                Console.WriteLine("Received incomplete header, closing connection.");
        //                return;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error while listening for messages: {ex.Message}");
        //    }
        //}


        //public async Task<string> ReceiveMessageAsync()
        //{
        //    var buffer = new byte[1024 * 4];
        //    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        //    if (result.MessageType == WebSocketMessageType.Text)
        //    {
        //        return Encoding.UTF8.GetString(buffer, 0, result.Count);
        //    }
        //    return null;
        //}

        // Listen for message from the TCP client
        public async Task<string> ReceiveMessageAsync()
        {
            var stream = _tcpClient.GetStream();

            // Đọc 4 byte độ dài
            byte[] lengthBuffer = new byte[4];
            int read = await stream.ReadAsync(lengthBuffer, 0, 4);
            if (read < 4)
                throw new IOException("Không đọc đủ 4 byte độ dài");

            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

            // Đọc message
            byte[] messageBuffer = new byte[messageLength];
            int totalRead = 0;
            while (totalRead < messageLength)
            {
                int bytesRead = await stream.ReadAsync(messageBuffer, totalRead, messageLength - totalRead);
                if (bytesRead == 0)
                    throw new IOException("Stream closed unexpectedly");
                totalRead += bytesRead;
            }
            Console.WriteLine($"[DEBUG] Expecting {messageLength} bytes, actually read {totalRead} bytes");
            string json = Encoding.UTF8.GetString(messageBuffer, 0, messageLength);
            return json;
        }

        public async Task SendMessageAsync(string message, string command = "")
        {
            byte[] messageBuffer = Encoding.UTF8.GetBytes(message);
            byte[] lengthBuffer = BitConverter.GetBytes(messageBuffer.Length);

            try
            {
                // First send the length of the message
                await stream.WriteAsync(lengthBuffer, 0, lengthBuffer.Length);

                // Then send the actual message
                await stream.WriteAsync(messageBuffer, 0, messageBuffer.Length);
                if (command == "SentprocessDump" || command == "SentprocessList") Console.WriteLine($"Sent message to client {Id}, Message length = {message.Length}");
                else Console.WriteLine($"Sent message to client {Id}: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message to client {Id}: {ex.Message}");
            }
        }

        public async Task CloseAsync()
        {
            if (_tcpClient != null && _tcpClient.Connected)
            {
                try
                {
                    await stream.FlushAsync();
                    _roomManager.RemoveClient(this.Id);
                    stream.Close();
                    _tcpClient.Close();
                    Console.WriteLine($"Connection closed for client {Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing connection for client {Id}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Client {Id} is already disconnected.");
            }
        }
    }
}
