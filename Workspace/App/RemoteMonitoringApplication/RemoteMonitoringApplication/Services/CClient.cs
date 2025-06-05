using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Net.Sockets;
using Org.BouncyCastle.Math.Field;
using System.IO;
using System.Text.Json;

namespace RemoteMonitoringApplication.Services
{
    public class CClient
    {
        //private readonly ClientWebSocket _client = new();
        private readonly TcpClient _client;
        private NetworkStream _stream;
        private readonly string _host;
        private readonly int _port;

        //private readonly Uri _serverUri;
        //private readonly CancellationToken _cancellationToken;

        public event Action<string> MessageReceived;

        public CClient(string host, int port)
        {
            _host = host;
            _port = port;
            _client = new TcpClient();
        }

        //public async Task ConnectAsync()
        //{
        //    try
        //    {
        //        if (_client.State != WebSocketState.Open)
        //        {
        //            await _client.ConnectAsync(_serverUri, CancellationToken.None);
        //            _ = Task.Run(ReceiveLoopAsync);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Windows.MessageBox.Show($"Error connecting to WebSocket server: {ex.Message}");
        //    }
        //}

        public async Task ConnectAsync()
        {
            try
            {
                await _client.ConnectAsync(_host, _port);
                _stream = _client.GetStream();
                _ = Task.Run(ReceiveLoopAsync);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error connecting to WebSocket server: {ex.Message}");
            }
        }

        //public async Task ReceiveLoopAsync()
        //{
        //    var buffer = new byte[1024 * 4];
        //    while (_client.State == WebSocketState.Open)
        //    {
        //        var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        //        if (result.MessageType == WebSocketMessageType.Close)
        //        {
        //            await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        //            break;
        //        }

        //        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
        //        MessageReceived?.Invoke(message);
        //    }
        //}

        public async Task ReceiveLoopAsync()
        {
            var lengthBuffer = new byte[4];

            try
            {
                while (_client.Connected)
                {
                    int totalRead = 0;
                    int lengthNeeded = 4;

                    // Đọc 4 byte đầu tiên để lấy độ dài message
                    while (totalRead < lengthNeeded)
                    {
                        int bytesRead = await _stream.ReadAsync(lengthBuffer, totalRead, lengthNeeded - totalRead);
                        if (bytesRead == 0)
                        {
                            // Kết nối đóng, thoát vòng lặp
                            Console.WriteLine("Connection closed by server.");
                            return;
                        }
                        totalRead += bytesRead;
                    }

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                    // Có thể kiểm tra messageLength ở đây nếu muốn

                    var messageBuffer = new byte[messageLength];
                    totalRead = 0;

                    // Đọc đủ messageLength bytes
                    while (totalRead < messageLength)
                    {
                        int bytesRead = await _stream.ReadAsync(messageBuffer, totalRead, messageLength - totalRead);
                        if (bytesRead == 0)
                        {
                            Console.WriteLine("Connection closed by server.");
                            return;
                        }
                        totalRead += bytesRead;
                    }

                    var message = Encoding.UTF8.GetString(messageBuffer, 0, messageLength).Trim();

                    // Xử lý message nhận được (ví dụ raise event, gọi callback, ...)
                    MessageReceived?.Invoke(message);
                }
            }
            catch (Exception ex)
            {
                // Xử lý lỗi, ví dụ log hoặc thông báo UI
                Console.WriteLine($"Error receiving message: {ex.Message}");
            }
        }

        //public async Task SendMessageAsync(string message)
        //{
        //    if (_client.State == WebSocketState.Open)
        //    {
        //        var buffer = Encoding.UTF8.GetBytes(message);
        //        var segment = new ArraySegment<byte>(buffer);
        //        await _client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        //    }
        //}

        public async Task SendMessageAsync(string message)
        {
            if (_client.Connected)
            {
                // Convert the message length to bytes and send it first
                var lengthBuffer = BitConverter.GetBytes(message.Length);
                await _stream.WriteAsync(lengthBuffer, 0, lengthBuffer.Length);

                // Convert the message to bytes and send it
                var messageBuffer = Encoding.UTF8.GetBytes(message);
                await _stream.WriteAsync(messageBuffer, 0, messageBuffer.Length);
            }
        }

        public async Task DisconnectAsync()
        {
            if (_client.Connected)
            {
                await _client.GetStream().FlushAsync();
                _stream?.Close();
                _client.Close();
                _client.Dispose();
                MessageReceived = null; // Unsubscribe from the event
                System.Windows.MessageBox.Show("Disconnected from the server.");
            }
        }
        public async Task SendFileAsync(string filePath, string command, string targetId)
        {
            // Tạo header
                long fileSize = new FileInfo(filePath).Length;

                // Tạo header đầy đủ với length
                var header = new
                {
                    command = "SentprocessDump",
                    target_id = targetId,
                    length = fileSize
                };
                string headerJson = JsonSerializer.Serialize(header);
                byte[] headerBytes = Encoding.UTF8.GetBytes(headerJson);
                byte[] headerLengthBytes = BitConverter.GetBytes(headerBytes.Length);

                // Gửi header length
                await _stream.WriteAsync(headerLengthBytes, 0, 4);

                // Gửi header
                await _stream.WriteAsync(headerBytes, 0, headerBytes.Length);


                // Gửi file
                using (FileStream fs = File.OpenRead("dumpTemp.dmp"))
                {
                    await fs.CopyToAsync(_stream);
                }

        }


        public async Task ReceiveFileAsync(string savePath)
        {
            byte[] sizeBuffer = new byte[8];
            await _stream.ReadAsync(sizeBuffer, 0, 8);
            long fileSize = BitConverter.ToInt64(sizeBuffer, 0);

            using (FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while (totalRead < fileSize &&
                       (bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                }
            }

            Console.WriteLine($"File received: {fileSize} bytes.");
        }

        public async Task RelayFileAsync( string pathFile, long fileSize)
        {
            byte[] buffer = new byte[8192];
            long totalRelayed = 0;
            int bytesRead;

            // Mở file để ghi
            using (FileStream fs = new FileStream(pathFile, FileMode.Create, FileAccess.Write))
            {
                while (totalRelayed < fileSize &&
                       (bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, bytesRead);
                    totalRelayed += bytesRead;
                }

                await fs.FlushAsync();
            }

            Console.WriteLine($"File saved to {pathFile}, total {totalRelayed} bytes.");
        }

    }
}
