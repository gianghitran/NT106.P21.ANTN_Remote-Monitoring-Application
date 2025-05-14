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
        public string email { get; set; }
        public string role { get; set; }
        //public string token { get; set; }

        private SessionManager() { }

        public void Clear()
        {
            id = null;
            username = null;
            email = null;
            role = null;
            //Token = null;
        }
    }
}
