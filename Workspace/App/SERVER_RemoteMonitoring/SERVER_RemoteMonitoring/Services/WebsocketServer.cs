using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Net;
using System.Threading;
using System.Windows;

namespace SERVER_RemoteMonitoring.Services
{
    public class WebsocketServer
    {
        private HttpListener _httpListener;
        private readonly AuthService _authservice;
        private readonly SessionManager _sessionManager;
        private readonly List<ClientConnectionWS> _clients = new List<ClientConnectionWS>();
        private const string uri = "http://localhost:8080/";

        public WebsocketServer(AuthService authService)
        {
            _authservice = authService;
            _sessionManager = new SessionManager();
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(uri);
        }

        public void Start()
        {
            _httpListener.Start();
            Console.WriteLine("WebSocket server started at " + uri);
            Task.Run(AcceptClientsAsync);
        }

        public async Task AcceptClientsAsync()
        {
            while (_httpListener.IsListening)
            {
                var context = await _httpListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var webSocketContext = await context.AcceptWebSocketAsync(null);
                    var client = new ClientConnectionWS(webSocketContext.WebSocket);
                    MessageBox.Show("Client connected: " + client.Id);
                    _clients.Add(client);
                    Console.WriteLine("Client connected: " + client.Id);
                    _ = Task.Run(() => HandleClient(client));
                }
            }
        }

        private async Task HandleClient(ClientConnectionWS client)
        {
            try
            {
                var handler = new ClientHandler(client, _authservice, _sessionManager);
                var authenticated = await handler.ProcessAsync();

                if (authenticated)
                {
                    Console.WriteLine("Client authenticated: " + client.Id);

                    var session = _sessionManager.GetSession(client.Id);
                    client.Session = session;

                    await client.SendMessageAsync("Welcome to the WebSocket server!");
                    await client.ListenForMessageAsync();
                }
                else
                {
                    Console.WriteLine("Client authentication failed: " + client.Id);
                    await client.SendMessageAsync("Authentication failed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client {client.Id}: {ex.Message}");
            }
            finally
            {
                //await client.CloseAsync();
                _sessionManager.RemoveSession(client.Id);
                _clients.Remove(client);
                Console.WriteLine("Client disconnected: " + client.Id);
            }
        }

        public async void Stop()
        {
            foreach (var client in _clients)
            {
                await client.CloseAsync();
            }
            _httpListener.Stop();
            Console.WriteLine("WebSocket server stopped");
        }
    }
}
