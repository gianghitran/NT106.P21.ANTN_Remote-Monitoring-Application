using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace RemoteMonitoringApplication.Services
{
    public class WebSocketConnectServer
    {
        private readonly WebSocketClient _client;

        public WebSocketConnectServer(string serverUri)
        {
            _client = new WebSocketClient(serverUri);
            _client.MessageReceived += HandleMessageReceived;
        }

        public async Task ConnectAsync()
        {
            await _client.ConnectAsync();
        }

        public async Task SendMessageAsync(string message)
        {
            await _client.SendMessageAsync(message);
        }

        private void HandleMessageReceived(string message)
        {
            // Handle the received message here
            Console.WriteLine($"Message received: {message}");
        }

        public async Task DisconnectAsync()
        {
            await _client.DisconnectAsync();
        }

    }
}
