using RemoteMonitoringApplication.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteMonitoringApplication.Services
{
    public class SessionManager
    {
        private static readonly Lazy<SessionManager> _instance = new(() => new SessionManager());
        public static SessionManager Instance => _instance.Value;

        public string id { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string email { get; set; }
        public string role { get; set; }
        public CClient tcpClient { get; set; }
        public string ClientId { get; set; }
        public string ClientPassword { get; set; }

        public string token { get; set; }

        private SessionManager() { }

        public void Clear()
        {
            id = null;
            username = null;
            password = null;
            email = null;
            role = null;
            tcpClient = null;
            ClientId = null;
            ClientPassword = null;
            token = null;
        }
    }
}
