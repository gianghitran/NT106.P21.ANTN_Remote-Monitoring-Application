using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SERVER_RemoteMonitoring.Models
{
    class UserLogin
    {
            [PrimaryKey, AutoIncrement]

            public int LoginID { get; set; }
            [NotNull]
            public string UserName { get; set; }
            public string UserId { get; set; }

            [NotNull]
            public string UserSessionID { get; set; } // e.g., "Controller", "Partner"
            public DateTime ConnectAt { get; set; }

            public UserLogin()
            {
                ConnectAt = DateTime.Now;
            }
        
    }
}
