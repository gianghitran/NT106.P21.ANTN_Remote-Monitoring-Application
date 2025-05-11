using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SERVER_RemoteMonitoring.Services
{
    public class SessionManager
    {
        private readonly Dictionary<string, UserSession> _sessions = new Dictionary<string, UserSession>();

        public void AddSession(string clientId, UserSession session)
        {
            _sessions[clientId] = session;
        }

        public UserSession GetSession(string clientId)
        {
            return _sessions.TryGetValue(clientId, out var session) ? session : null;
        }

        public void RemoveSession(string clientId)
        {
            if (_sessions.ContainsKey(clientId))
            {
                _sessions.Remove(clientId);
            }
        }
    }
}
