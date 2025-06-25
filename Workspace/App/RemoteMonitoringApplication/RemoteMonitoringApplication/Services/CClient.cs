using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Net.Sockets;
using Org.BouncyCastle.Math.Field;
using System.IO;
using System.Text.Json;
using System.Collections.Concurrent;

namespace RemoteMonitoringApplication.Services
{
    public class CClient
    {
        private TcpClient _client; // Bỏ readonly
        private NetworkStream _stream;
        private readonly string _host;
        private readonly int _port;

        private bool _isReconnecting = false;
        private bool _disposed = false;
        private CancellationTokenSource _cts; // Để huỷ ReceiveLoop khi disconnect

        public event Action Disconnected;
        public event Action<string> MessageReceived;

        public bool IsReconnecting
        {
            get => _isReconnecting;
            set => _isReconnecting = value;
        }
        public NetworkStream Stream => _stream;

        public string Id { get; set; }

        public CClient(string host, int port)
        {
            _host = host;
            _port = port;
            _client = new TcpClient();
            IsReconnecting = false;
            _disposed = false;
        }

        public async Task ConnectAsync()
        {
            try
            {
                _cts = new CancellationTokenSource();
                await _client.ConnectAsync(_host, _port);
                _stream = _client.GetStream();
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error connecting to TCP server: Server not found");
                throw new Exception("Failed to connect to TCP server", ex);
            }
        }

        public async Task ReceiveLoopAsync(CancellationToken token)
        {
            var lengthBuffer = new byte[4];

            try
            {
                while (_client.Connected)
                {
                    int totalRead = 0;
                    int lengthNeeded = 4;

                    while (totalRead < lengthNeeded)
                    {
                        int bytesRead = await _stream.ReadAsync(lengthBuffer.AsMemory(totalRead, lengthNeeded - totalRead), token);
                        if (bytesRead == 0) return; // Connection closed
                        totalRead += bytesRead;
                    }

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                    var messageBuffer = new byte[messageLength];
                    totalRead = 0;
                    while (totalRead < messageLength)
                    {
                        int bytesRead = await _stream.ReadAsync(messageBuffer.AsMemory(totalRead, messageLength - totalRead), token);
                        if (bytesRead == 0) return; // Connection closed
                        totalRead += bytesRead;
                    }

                    var message = Encoding.UTF8.GetString(messageBuffer);
                    MessageReceived?.Invoke(message);
                }
            }
            catch (Exception ex)
            {
                if (!_disposed && !token.IsCancellationRequested)
                {
                    DispatcherHelper.RunOnUI(() =>
                    {
                        System.Windows.MessageBox.Show("Error receiving message: " + ex.Message);
                    });
                    Disconnected?.Invoke();
                }
            }
        }

        // public async Task SendMessageAsync(string message)
        // {
        //     if (_client.Connected)
        //     {
        //         var lengthBuffer = BitConverter.GetBytes(message.Length);
        //         await _stream.WriteAsync(lengthBuffer, 0, lengthBuffer.Length);

        //         var messageBuffer = Encoding.UTF8.GetBytes(message);
        //         await _stream.WriteAsync(messageBuffer, 0, messageBuffer.Length);
        //     }
        // }
        public async Task SendMessageAsync(string message)
        {
            if (_client.Connected)
            {
                var messageBuffer = Encoding.UTF8.GetBytes(message);
                var lengthBuffer = BitConverter.GetBytes(messageBuffer.Length);

                await _stream.WriteAsync(lengthBuffer, 0, lengthBuffer.Length);
                await _stream.WriteAsync(messageBuffer, 0, messageBuffer.Length);
                await _stream.FlushAsync(); // đảm bảo gửi sạch
            }
        }

        public async Task SendMessageAsync(string from, string to, object payload)
        {
            var packet = new
            {
                from = from,
                to = to,
                payload = payload
            };
            string wrappedJson = JsonSerializer.Serialize(packet);
            Console.WriteLine($"Sending message: {wrappedJson}");
            await SendMessageAsync(wrappedJson);
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _disposed = true;
                _cts?.Cancel();

                if (_client != null)
                {
                    try
                    {
                        // Đảm bảo shutdown cả hai chiều để LB phát hiện EOF
                        _client.Client.Shutdown(SocketShutdown.Both);
                    }
                    catch { }
                }

                if (_stream != null)
                {
                    try { await _stream.FlushAsync(); } catch { }
                    try { _stream.Close(); } catch { }
                    try { _stream.Dispose(); } catch { }
                    _stream = null;
                }

                if (_client != null)
                {
                    try { _client.Close(); } catch { }
                    try { _client.Dispose(); } catch { }
                    _client = null;
                }

                Disconnected?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Disconnect error: " + ex.Message);
            }
        }


        public int Port
        {
            get
            {
                try
                {
                    if (_client != null && _client.Connected)
                    {
                        // Lấy port của server đang kết nối (remote endpoint)
                        return ((System.Net.IPEndPoint)_client.Client.RemoteEndPoint).Port;
                    }
                }
                catch { }
                return -1;
            }
        }

        public static class DispatcherHelper
        {
            public static void RunOnUI(Action action)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(action);
            }
        }
        private const string delimiter = "|;|";
        public async Task RelayFileAsync(Stream sourceStream, string outputFilePath, long fileSize)
        {
            byte[] buffer = new byte[8192];
            List<byte> receivedBytes = new();
            int bytesRead;

            ConcurrentDictionary<int, string> chunks = new ConcurrentDictionary<int, string>();

            string endSignal = "<END - EOF>";

            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                receivedBytes.AddRange(buffer.Take(bytesRead));

                // Tách các chunk nếu có delimiter
                while (true)
                {
                    string currentData = Encoding.UTF8.GetString(receivedBytes.ToArray());

                    int delimIndex = currentData.IndexOf(delimiter);
                    if (delimIndex < 0)
                        break;

                    // Đọc chunk theo định dạng: fileID|||chunkData|||index|||
                    // Tìm vị trí của 4 delimiter (chia thành 4 phần)
                    string[] parts = currentData.Split(new string[] { delimiter }, 4, StringSplitOptions.None);

                    if (parts.Length < 4) break; // chờ dữ liệu đủ

                    string recvFileID = parts[0];
                    string chunkData = parts[1];
                    string chunkIndexStr = parts[2];
                    string rest = parts[3];

                    //if (recvFileID != fileID)
                    //{
                    //    Console.WriteLine("FileID không khớp, bỏ qua chunk này");
                    //    // Loại bỏ phần đã đọc (phần này + delimiter)
                    //    int lenToRemove = Encoding.UTF8.GetByteCount(parts[0] + delimiter + parts[1] + delimiter + parts[2] + delimiter);
                    //    receivedBytes.RemoveRange(0, lenToRemove);
                    //    continue;
                    //}

                    if (chunkData == "<END - EOF>")
                    {
                        // Đã hết file, nối lại toàn bộ chunk và ghi file
                        var orderedChunks = chunks.OrderBy(kv => kv.Key).Select(kv => kv.Value);
                        string base64File = string.Concat(orderedChunks);
                        byte[] fileBytes = Convert.FromBase64String(base64File);

                        await File.WriteAllBytesAsync(outputFilePath, fileBytes);
                        Console.WriteLine($"Đã nhận file xong, lưu tại {outputFilePath}");
                        return;
                    }

                    if (!int.TryParse(chunkIndexStr, out int chunkIndex))
                    {
                        Console.WriteLine("Chunk index không hợp lệ");
                        break;
                    }

                    // Lưu chunk vào dictionary
                    chunks.TryAdd(chunkIndex, chunkData);

                    // Xóa phần đã đọc khỏi buffer
                    int bytesToRemove = Encoding.UTF8.GetByteCount(parts[0] + delimiter + parts[1] + delimiter + parts[2] + delimiter);
                    receivedBytes.RemoveRange(0, bytesToRemove);
                }
            }
        }


        public async Task SendFileAsync(string filePath, string command, string targetId)
        {
            string fileID = Guid.NewGuid().ToString();
            byte[] buffer = new byte[8192];
            List<string> chunks = new();
            int bytesRead;
            long fileSize = new FileInfo(filePath).Length;

            // 1. Gửi JSON header
            var header = new
            {
                command = "SentprocessDump",
                target_id = targetId,
                status = "success",
                length = fileSize
            };

            string headerJson = JsonSerializer.Serialize(header) + "\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(headerJson);
            await _stream.WriteAsync(headerBytes, 0, headerBytes.Length);

            // 2. Đọc file, chuyển thành base64 chunk
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    string base64Chunk = Convert.ToBase64String(buffer, 0, bytesRead);
                    chunks.Add(base64Chunk);
                }
            }

            // 3. Gửi từng chunk dạng string: fileID + delimiter + base64Chunk + delimiter + index + delimiter
            int counter = 0;
            foreach (string chunk in chunks)
            {
                string formattedChunk = $"{fileID}{delimiter}{chunk}{delimiter}{counter}{delimiter}";
                byte[] chunkBytes = Encoding.UTF8.GetBytes(formattedChunk);

                await _stream.WriteAsync(chunkBytes, 0, chunkBytes.Length);

                counter++;
                await Task.Delay(30); // nếu muốn giới hạn tốc độ
            }

            // 4. Gửi chunk kết thúc
            string endSignal = $"{fileID}{delimiter}<END - EOF>{delimiter}{counter}{delimiter}";
            byte[] endSignalBytes = Encoding.UTF8.GetBytes(endSignal);
            await _stream.WriteAsync(endSignalBytes, 0, endSignalBytes.Length);

            Console.WriteLine($"Đã gửi file: {filePath} với {counter} chunks.");


        }
    }
}
