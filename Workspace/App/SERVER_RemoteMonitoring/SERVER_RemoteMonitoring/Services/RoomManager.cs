using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SERVER_RemoteMonitoring.Services
{
    public class RoomManager
    {
        // Lưu thông tin ID và password tạm thời
        private readonly Dictionary<string, string> _clientPasswords = new Dictionary<string, string>();
        // Lưu các phòng và danh sách client
        private readonly Dictionary<string, List<ClientConnectionWS>> _rooms = new Dictionary<string, List<ClientConnectionWS>>();

        public void RegisterClient(string id, string password)
        {
            _clientPasswords[id] = password;
        }

        public bool VerifyClient(string id, string password)
        {
            return _clientPasswords.TryGetValue(id, out var pw) && pw == password;
        }

        public void JoinRoom(string roomId, ClientConnectionWS client)
        {
            if (!_rooms.ContainsKey(roomId))
                _rooms[roomId] = new List<ClientConnectionWS>();
            if (!_rooms[roomId].Contains(client))
                _rooms[roomId].Add(client);
        }

        public List<ClientConnectionWS> GetRoomClients(string roomId)
        {
            return _rooms.TryGetValue(roomId, out var clients) ? clients : new List<ClientConnectionWS>();
        }
    }
}