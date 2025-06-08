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
                while (_tcpClient.Connected)
                {

                    // Read first 4 bytes to get the length of the incoming message
                    byte[] lengthBuffer = new byte[4];
                    int totalRead = 0;
                    int lengthBytesNeeded = 4;

                    // Read the length of the message until we have 4 bytes
                    while (totalRead < lengthBytesNeeded)
                    {
                        int bytesRead = await stream.ReadAsync(lengthBuffer, totalRead, lengthBytesNeeded - totalRead);
                        if (bytesRead == 0)
                        {
                            // Connection closed
                            Console.WriteLine("Connection closed by client.");
                            return;
                        }
                        totalRead += bytesRead;
                    }

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                    // Read all message bytes based on the length
                    byte[] messageBuffer = new byte[messageLength];
                    totalRead = 0;

                    while (totalRead < messageLength)
                    {
                        int bytesRead = await stream.ReadAsync(messageBuffer, totalRead, messageLength - totalRead);
                        if (bytesRead == 0)
                        {
                            // Connection closed
                            Console.WriteLine("Connection closed by client.");
                            return;
                        }

                        totalRead += bytesRead;
                    }

                    var jsonMessage = Encoding.UTF8.GetString(messageBuffer, 0, messageLength).Trim();

                    if (Handler != null)
                    {
                        Console.WriteLine($"Received message from client {Id}: {jsonMessage}");
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
            byte[] lengthBuffer = new byte[4];
            int totalRead = 0;
            int lengthBytesNeeded = 4;

            // Read the length of the message
            while (totalRead < lengthBytesNeeded)
            {
                int bytesRead = await stream.ReadAsync(lengthBuffer, totalRead, lengthBytesNeeded - totalRead);
                if (bytesRead == 0)
                {
                    // Connection closed
                    Console.WriteLine("Connection closed by client.");
                    return null;
                }
                totalRead += bytesRead;
            }

            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
            byte[] messageBuffer = new byte[messageLength];
            totalRead = 0;

            // Read the message bytes
            while (totalRead < messageLength)
            {
                int bytesRead = await stream.ReadAsync(messageBuffer, totalRead, messageLength - totalRead);
                if (bytesRead == 0)
                {
                    // Connection closed
                    Console.WriteLine("Connection closed by client.");
                    return null;
                }
                totalRead += bytesRead;
            }

            var jsonMessage = Encoding.UTF8.GetString(messageBuffer, 0, messageLength).Trim();
            //Console.WriteLine($"Received message from client {Id}: {jsonMessage}");
            Console.WriteLine($"Received message from client {Id}.");

            if (Handler != null)
            {
                //await Handler.HandleMessageAsync(jsonMessage);
                return jsonMessage; // Return the message after handling
            }

            return null;
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
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);

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

                await stream.WriteAsync(chunkBytes, 0, chunkBytes.Length);

                counter++;
                await Task.Delay(30); // nếu muốn giới hạn tốc độ
            }

            // 4. Gửi chunk kết thúc
            string endSignal = $"{fileID}{delimiter}<END - EOF>{delimiter}{counter}{delimiter}";
            byte[] endSignalBytes = Encoding.UTF8.GetBytes(endSignal);
            await stream.WriteAsync(endSignalBytes, 0, endSignalBytes.Length);

            Console.WriteLine($"Đã gửi file: {filePath} với {counter} chunks.");


        }
    }
}
