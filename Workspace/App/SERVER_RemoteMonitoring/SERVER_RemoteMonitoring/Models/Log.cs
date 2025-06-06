using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace SERVER_RemoteMonitoring.Models
{
    public class Log
    {
        [PrimaryKey, AutoIncrement]
       
        public int logid { get; set; }
        [NotNull]
        public string UserId { get; set; }

        [NotNull]
        public string Role { get; set; } // e.g., "Controller", "Partner"
        public string PartnerId { get; set; } 
        public string Action { get; set; } 
        public DateTime LogAt { get; set; }

        public Log()
        {
            LogAt = DateTime.UtcNow;
        }

        //public ICollection<Log> Logs { get; set; }
    }
}
