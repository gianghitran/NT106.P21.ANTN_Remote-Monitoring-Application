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
using System.Text.Json;

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
                System.Windows.MessageBox.Show($"Error connecting to WebSocket server: {ex.Message}");
            }
        }

        public async Task ReceiveLoopAsync(CancellationToken token)
        {
            var lengthBuffer = new byte[4];

            try
            {
                while (_client.Connected && !token.IsCancellationRequested)
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
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            try { _client?.Dispose(); } catch { }
            _stream = null;
            _client = null;
            _disposed = true;
            _cts?.Cancel();
            Disconnected?.Invoke();
        }

        public int Port => _port;

        public static class DispatcherHelper
        {
            public static void RunOnUI(Action action)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(action);
            }
        }
    }
}
