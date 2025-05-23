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
        // Mỗi target client có duy nhất một controller
        private readonly Dictionary<string, ClientConnectionWS> _targetToController = new Dictionary<string, ClientConnectionWS>(); // targetId → controller connection

        // Mỗi controller có thể điều khiển nhiều client
        private readonly Dictionary<ClientConnectionWS, List<string>> _controllerToTargets = new Dictionary<ClientConnectionWS, List<string>>(); // controller → list of targetIds

        private readonly Dictionary<string, ClientConnectionWS> _idToClient = new Dictionary<string, ClientConnectionWS>();

        private readonly Dictionary<string, UserSession> _idToSession = new Dictionary<string, UserSession>();

        public async Task RegisterClient(string id, string password, ClientConnectionWS client, UserSession session)
        {
            _clientPasswords[id] = password;
            _idToClient[id] = client;
            _idToSession[id] = session;
            session.tempId = id; // id là chuỗi random từ client
        }

        public bool VerifyClient(string id, string password)
        {
            if (_clientPasswords.TryGetValue(id, out var stored))
                return stored == password;
            return false;
        }

        public async Task<bool> JoinRoom(string targetId, ClientConnectionWS controller)
        {
            if (!VerifyClient(targetId, _clientPasswords[targetId]))
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

        public ClientConnectionWS GetClientById(string id)
        {
            return _idToClient.TryGetValue(id, out var client) ? client : null;
        }

        public UserSession GetSessionById(string id)
        {
            _idToSession.TryGetValue(id, out var session);
            return session;
        }


    }
}