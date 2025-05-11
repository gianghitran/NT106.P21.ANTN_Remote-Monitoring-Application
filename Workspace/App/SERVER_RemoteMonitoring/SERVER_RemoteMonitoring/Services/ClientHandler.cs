using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace SERVER_RemoteMonitoring.Services
{
    class ClientHandler
    {
        private readonly ClientConnectionWS _client;
        private readonly AuthService _authService;
        private readonly SessionManager _sessionManager;

        public ClientHandler(ClientConnectionWS clientConnection, AuthService authService, SessionManager sessionManager)
        {
            _client = clientConnection;
            _authService = authService;
            _sessionManager = sessionManager;
        }

        public async Task<bool> ProcessAsync()
        {
            try
            {
                bool authenticated = await AuthenticateClientAsync();
                if (!authenticated) return false;

                //await ListenMessageAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing client {_client.Id}: {ex.Message}");
                return false;
            }
            finally
            {
                await _client.CloseAsync();
                Console.WriteLine($"Client {_client.Id} disconnected.");
            }
        }

        private async Task<bool> AuthenticateClientAsync()
        {
            while (true)
            {
                var message = await _client.ReceiveMessageAsync();
                var jsonMessage = System.Text.Json.JsonSerializer.Deserialize<BaseMessage>(message);

                if (jsonMessage == null)
                {
                    await _client.SendMessageAsync("");
                    continue;
                }

                var command = jsonMessage.command;

                switch (command.ToUpperInvariant())
                {
                    case "LOGIN":
                        var loginData = System.Text.Json.JsonSerializer.Deserialize<LoginMessage>(message);
                        return await HandleLoginAsync(loginData);

                    case "REGISTER":
                        var registerData = System.Text.Json.JsonSerializer.Deserialize<RegisterMessage>(message);
                        await HandleRegisterAsync(registerData);
                        break;

                    default:
                        return await UnknownCommand(command);
                }
            }
        }

        private async Task HandleRegisterAsync(RegisterMessage data)
        {
            if (string.IsNullOrEmpty(data.username) || string.IsNullOrEmpty(data.email) || string.IsNullOrEmpty(data.password))
            {
                await _client.SendMessageAsync("ERROR|Invalid registration format.");
                return;
            }

            var username = data.username;
            var email = data.email;
            var password = data.password; 

            bool success = await _authService.RegisterAsync(username, email, password);

            if (success)
            {
                await _client.SendMessageAsync("SUCCESS|Registration successful.");
            }
            else
            {
                await _client.SendMessageAsync("ERROR|User already exists.");
            }
        }

        private async Task<bool> HandleLoginAsync(LoginMessage data)
        {
            if (!string.IsNullOrEmpty(data.username)) {
                await _client.SendMessageAsync("ERROR|Invalid login format.");
                return false;
            }

            var username = data.username;
            var password = data.password;

            bool success = await _authService.LoginAsync(username, password);
            if (success)
            {
                var user = await _authService.GetUserAsync(username);
                if (user == null)
                {
                    await _client.SendMessageAsync("ERROR|User not found.");
                    return false;
                }

                var session = new UserSession
                {
                    userId = user.Id,
                    email = user.Email,
                    role = user.Role,
                };

                _sessionManager.AddSession(_client.Id, session);
                await _client.SendMessageAsync("SUCCESS|Login successful.");
                return true;
            }
            else
            {
                await _client.SendMessageAsync("ERROR|Invalid username or password.");
                return false;
            }
        }

        private async Task<bool> UnknownCommand(string command)
        {
            await _client.SendMessageAsync($"ERROR|Unknown command: {command}");
            return false;
        }

        //private async Task ListenMessageAsync()
        //{
        //    _client.MessageReceived += (msg) =>
        //    {
        //        Console.WriteLine($"Message from client {_client.Id}: {msg}");
        //        // Handle incoming messages here
        //    };

        //    await _client.ListenForMessageAsync();
        //}

        // Define a base message json 
        private class BaseMessage
        {
            public string command { get; set; }
        }

        private class LoginMessage : BaseMessage
        {
            public string username { get; set; }
            public string password { get; set; }
        }

        // Define a class to represent the expected JSON structure
        private class RegisterMessage : BaseMessage
        {
            public string username { get; set; }
            public string email { get; set; }
            public string password { get; set; }
        }
    }
}
