using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace RemoteMonitoringApplication.Services
{
    public class AuthService
    {
        private readonly CClient _client;

        private event Action<string> MessageReceived;

        public event Action<LoginMessage>? LoginSuccess;
        public event Action<string, string>? LoginFailed;

        public event Action? RegisterSuccess;
        public event Action<string, string>? RegisterFailed;

        public AuthService(CClient client)
        {
            _client = client;
            _client.MessageReceived += HandleMessageReceived;
        }

        public async Task RegisterAsync(string username, string email, string password)
        {
            var request = new RegisterRequest
            {
                command = "register",
                username = username,
                email = email,
                password = password
            };

            var jsonRequest = System.Text.Json.JsonSerializer.Serialize(request);
            await _client.SendMessageAsync(jsonRequest);
         
        }

        public async Task LoginAsync(string username, string password)
        {
            var request = new LoginRequest
            {
                command = "login",
                username = username,
                password = password
            };
            var jsonRequest = System.Text.Json.JsonSerializer.Serialize(request);
            await _client.SendMessageAsync(jsonRequest);
        }

        public void HandleMessageReceived(string message)
        {
            try
            {
                var response = JsonSerializer.Deserialize<BaseResponse<JsonElement>>(message);
                Console.WriteLine(response);

                if (response == null || response.status == null) return;

                switch (response.command)
                {
                    case "register":
                        HandleRegisterResponse(response, message);
                        break;
                    case "login":
                        HandleLoginResponse(response, message);
                        break;
                    default:
                        Console.WriteLine($"Unknown command: {response.command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing message: {ex.Message}");
            }
        }

        public void HandleRegisterResponse(BaseResponse<JsonElement> response, string message)
        {
            if (response.status == "success")
            {
                RegisterSuccess?.Invoke();
            }
            else if (response.status == "error")
            {
                var errorResponse = System.Text.Json.JsonSerializer.Deserialize<BaseResponse<string>>(message);
                if (errorResponse != null && errorResponse.message != null)
                {
                    RegisterFailed?.Invoke(errorResponse.status ,errorResponse.message);
                }
            }
            else
            {
                var failResponse = System.Text.Json.JsonSerializer.Deserialize<BaseResponse<string>>(message);
                if (failResponse != null && failResponse.message != null)
                {
                    RegisterFailed?.Invoke(failResponse.status, failResponse.message);
                }
            }
        }

        public void HandleLoginResponse(BaseResponse<JsonElement> response, string message)
        {
            if (response.status == "success")
            {
                var messageJson = response.message.GetRawText();
                LoginMessage? successResponse = JsonSerializer.Deserialize<LoginMessage>(messageJson);
                if (successResponse == null)
                {
                    Console.WriteLine("Received null login message");
                    return;
                }
                LoginSuccess?.Invoke(successResponse);
                _client.MessageReceived -= HandleMessageReceived; // Unsubscribe from the event after successful login
                Console.WriteLine($"Login successful for user: {successResponse.username}");
            }
            else if (response.status == "error")
            {
                var errorResponse = System.Text.Json.JsonSerializer.Deserialize<BaseResponse<string>>(message);
                if (errorResponse != null && errorResponse.message != null)
                {
                    LoginFailed?.Invoke(errorResponse.status ,errorResponse.message);
                }
            }
            else {
                var failResponse = System.Text.Json.JsonSerializer.Deserialize<BaseResponse<string>>(message);
                if (failResponse != null && failResponse.message != null)
                {
                    LoginFailed?.Invoke(failResponse.status, failResponse.message);
                }
            }
        }

        public class BaseRequest
        {
            [JsonPropertyName("command")]
            public string command { get; set; }
        }

        public class BaseResponse<T>
        {
            [JsonPropertyName("status")]
            public string status { get; set; }

            [JsonPropertyName("command")]
            public string command { get; set; }

            [JsonPropertyName("message")]
            public T message { get; set; }
        }

        public class LoginMessage
        {
            [JsonPropertyName("id")]
            public string id { get; set; }

            [JsonPropertyName("username")]
            public string username { get; set; }

            [JsonPropertyName("email")]
            public string email { get; set; }

            [JsonPropertyName("role")]
            public string role { get; set; }

            [JsonPropertyName("token")]
            public string token { get; set; }
        }

        private class RegisterRequest : BaseRequest 
        {
            [JsonPropertyName("username")]
            public string username { get; set; }

            [JsonPropertyName("email")]
            public string email { get; set; }

            [JsonPropertyName("password")]
            public string password { get; set; }
        }

        private class LoginRequest : BaseRequest
        {
            [JsonPropertyName("username")]
            public string username { get; set; }

            [JsonPropertyName("password")]
            public string password { get; set; }
        }
    }
}
