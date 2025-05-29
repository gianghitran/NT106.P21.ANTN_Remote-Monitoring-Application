using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace RemoteMonitoringApplication.Services
{
    public class ConnectServer
    {
        private readonly CClient _client;

        public ConnectServer()
        {
            _client = new CClient("localhost", 8080); // Default host and port
            _client.MessageReceived += HandleMessageReceived;
        }

        public ConnectServer(string host, int port)
        {
            _client = new CClient(host, port);
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

        public CClient GetClient()
        {
            return _client;
        }

        public async Task DisconnectAsync()
        {
            await _client.DisconnectAsync();
        }

    }
}
