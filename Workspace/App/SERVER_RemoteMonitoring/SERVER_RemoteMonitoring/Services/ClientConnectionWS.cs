using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SERVER_RemoteMonitoring.Services
{
    public class ClientConnectionWS
    {
        public WebSocket _webSocket { get; private set; }
        public string Id { get; private set; }
        public UserSession Session { get; set; } // User session associated with this connection

        public event Action<string> MessageReceived;

        public string IP { get; set; } // Client IP address

        public ClientHandler Handler { get; set; }

        public ClientConnectionWS(WebSocket webSocket, string IP) 
        {
            _webSocket = webSocket;
            Id = Guid.NewGuid().ToString(); // Unique per client
        }

        public async Task ListenForMessageAsync()
        {
            var buffer = new byte[1024 * 4];
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                //Console.WriteLine("XXXXXX Received message from client " + Id + ": " + result.MessageType.ToString());
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var jsonMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Received message from client {Id}: {jsonMessage}");
                    if (Handler != null)
                        Console.WriteLine("Handler");
                        await Handler.HandleMessageAsync(jsonMessage);
                        
                    
                    //var message = System.Text.Json.JsonSerializer.Deserialize<string>(jsonMessage);

                    //await HandleClientCommandAsync(message);

                    //MessageReceived?.Invoke(message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
        }

        public async Task<string> ReceiveMessageAsync()
        {
            var buffer = new byte[1024 * 4];
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                return Encoding.UTF8.GetString(buffer, 0, result.Count);
            }
            return null;
        }

        public async Task SendMessageAsync(string message)
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                var segment = new ArraySegment<byte>(buffer);
                await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public async Task CloseAsync()
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                _webSocket.Dispose();
            }
        }
    }
}
