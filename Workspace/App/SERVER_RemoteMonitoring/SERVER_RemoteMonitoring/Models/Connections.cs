using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SERVER_RemoteMonitoring.Models
{
    class Connections
    {
        [PrimaryKey, AutoIncrement]

        public int ConnectID { get; set; }
        [NotNull]
        public string UserName { get; set; }
        public string UserId { get; set; }

        [NotNull]
        public string Role { get; set; } // e.g., "Controller", "Partner"
        public string PartnerName { get; set; } // Name of the partner
        public string PartnerId { get; set; }
        public DateTimeOffset ConnectAt { get; set; }

        public Connections()
        {
            ConnectAt = DateTimeOffset.Now;;
        }
    }
}
