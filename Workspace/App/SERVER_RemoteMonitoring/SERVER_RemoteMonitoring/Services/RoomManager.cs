using SERVER_RemoteMonitoring.Data;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using SERVER_RemoteMonitoring.Models;
using System;

namespace SERVER_RemoteMonitoring.Services
{
    public class RoomManager
    {
        private readonly DatabaseService _dbService;

        public RoomManager(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public void RemoveClient(string id)
        {
            Console.WriteLine($"[RoomManager] Removing client {id}");
            _idToClient.Remove(id);
            _idToSession.Remove(id);
            _clientPasswords.Remove(id);
        }


        // Lưu thông tin ID và password tạm thời
        private readonly Dictionary<string, string> _clientPasswords = new Dictionary<string, string>();
        // Mỗi target client có duy nhất một controller
        private readonly Dictionary<string, TCPClient> _targetToController = new Dictionary<string, TCPClient>(); // targetId → controller connection

        // Mỗi controller có thể điều khiển nhiều client
        private readonly Dictionary<TCPClient, List<string>> _controllerToTargets = new Dictionary<TCPClient, List<string>>(); // controller → list of targetIds

        private readonly Dictionary<string, TCPClient> _idToClient = new Dictionary<string, TCPClient>();

        private readonly Dictionary<string, UserSession> _idToSession = new Dictionary<string, UserSession>();

        public async Task RegisterClient(string id, string password, TCPClient client, UserSession session, int serverPort)
        {
            var db = _dbService.GetDataBaseConnection();
            var existing = await db.Table<RoomClient>().Where(r => r.Id == id).FirstOrDefaultAsync();
            if (existing == null)
            {
                await db.InsertAsync(new RoomClient { Id = id, Password = password, ServerPort = serverPort });
            }
            else
            {
                existing.Password = password;
                existing.ServerPort = serverPort;
                await db.UpdateAsync(existing);
            }

            _clientPasswords[id] = password;
            _idToClient[id] = client; // id tạm (database id)
            _idToClient[client.Id] = client; // session id (GUID)
            _idToSession[id] = session;
            _idToSession[client.Id] = session;
            session.tempId = id;
        }


        public async Task<bool> VerifyClient(string id, string password)
        {
            var db = _dbService.GetDataBaseConnection();
            var existing = await db.Table<RoomClient>().Where(r => r.Id == id).FirstOrDefaultAsync();
            return existing != null && existing.Password == password;
        }

        public async Task<bool> JoinRoom(string targetId, TCPClient controller, string targetPassword)
        {
            // Kiểm tra targetId và password trong database
            if (!await VerifyClient(targetId, targetPassword))
                return false;

            // Gán controller cho target
            _targetToController[targetId] = controller;
            // Lưu list target dưới controller
            if (!_controllerToTargets.TryGetValue(controller, out var list))
            {
                list = new List<string>();
                _controllerToTargets[controller] = list;
            }
            if (!list.Contains(targetId))
                list.Add(targetId);
            return true;
        }

        public TCPClient GetClientById(string id)
        {
            return _idToClient.TryGetValue(id, out var client) ? client : null;
        }

        public UserSession GetSessionById(string id)
        {
            if (_idToSession.TryGetValue(id, out var session))
            {
                return session;
            }

            var db = _dbService.GetDataBaseConnection();
            var roomClient = db.Table<RoomClient>().Where(r => r.Id == id).FirstOrDefaultAsync().Result;
            if (roomClient != null)
            {
                return new UserSession
                {
                    tempId = roomClient.Id,
                    username = "",
                    email = "",
                    role = "User"
                };
            }

            return null;
        }



    }
}