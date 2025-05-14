using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SERVER_RemoteMonitoring.Services
{
    public class UserSession
    {
        public string username { get; set; }
        public string email { get; set; }
        public string role { get; set; } // e.g., "Admin", "User"
        public int id { get; set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(username);

        public void Clear()
        {
            username = null;
            email = null;
            role = null;
            id = 0;
        }

    }
}
