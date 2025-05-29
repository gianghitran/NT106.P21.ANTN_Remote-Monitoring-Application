using Org.BouncyCastle.Math.Field;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
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

        public TCPClient(TcpClient client)
        {
            _tcpClient = client;
            stream = _tcpClient.GetStream();
            Id = Guid.NewGuid().ToString(); // Unique per client
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
            Console.WriteLine($"Received message from client {Id}: {jsonMessage}");
            if (Handler != null)
            {
                //await Handler.HandleMessageAsync(jsonMessage);
                return jsonMessage; // Return the message after handling
            }

            return null;
        }

        public async Task SendMessageAsync(string message)
        {
            byte[] messageBuffer = Encoding.UTF8.GetBytes(message);
            byte[] lengthBuffer = BitConverter.GetBytes(messageBuffer.Length);

            try
            {
                // First send the length of the message
                await stream.WriteAsync(lengthBuffer, 0, lengthBuffer.Length);

                // Then send the actual message
                await stream.WriteAsync(messageBuffer, 0, messageBuffer.Length);
                Console.WriteLine($"Sent message to client {Id}: {message}");
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
