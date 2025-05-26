using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RemoteMonitoringApplication.Services
{
    public class WebSocketClient
    {
        private readonly ClientWebSocket _client = new();
        private readonly Uri _serverUri;
        private readonly CancellationToken _cancellationToken;

        public event Action<string> MessageReceived;

        public WebSocketClient(string serverUri)
        {
            _serverUri = new Uri(serverUri);
        }

        public async Task ConnectAsync()
        {
            try
            {
                if (_client.State != WebSocketState.Open)
                {
                    await _client.ConnectAsync(_serverUri, CancellationToken.None);
                    _ = Task.Run(ReceiveLoopAsync);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error connecting to WebSocket server: {ex.Message}");
            }
        }

        public async Task ReceiveLoopAsync()
        {
            var buffer = new byte[1024 * 4];
            while (_client.State == WebSocketState.Open)
            {
                var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                MessageReceived?.Invoke(message);
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (_client.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                var segment = new ArraySegment<byte>(buffer);
                await _client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public async Task DisconnectAsync()
        {
            if (_client.State == WebSocketState.Open)
            {
                await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
    }
}
