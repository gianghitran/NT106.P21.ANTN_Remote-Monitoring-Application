using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Net.Sockets;
using Org.BouncyCastle.Math.Field;

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
                    // Read the length of the incoming message
                    int totalRead = 0;
                    int lengthNeeded = 4;

                    while (totalRead < lengthNeeded)
                    {
                        int bytesRead = await _stream.ReadAsync(lengthBuffer, totalRead, lengthNeeded - totalRead);
                        if (bytesRead == 0) return; // Connection closed
                        totalRead += bytesRead;
                    }

                    // Convert the length from bytes to an integer
                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                    var messageBuffer = new byte[messageLength];
                    totalRead = 0;
                    while (totalRead < messageLength)
                    {
                        int bytesRead = await _stream.ReadAsync(messageBuffer, totalRead, messageLength - totalRead);
                        if (bytesRead == 0) return; // Connection closed
                        totalRead += bytesRead;
                    }

                    var message = Encoding.UTF8.GetString(messageBuffer);
                    MessageReceived?.Invoke(message);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error receiving message: {ex.Message}");
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
    }
}
