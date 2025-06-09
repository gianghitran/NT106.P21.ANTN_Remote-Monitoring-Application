using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace SERVER_RemoteMonitoring.Models
{
    public class User
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Unique, NotNull]
        public string Username { get; set; }
        public string Password { get; set; }

        [Unique, NotNull]
        public string Email { get; set; }
        public string Role { get; set; } // e.g., "Admin", "User"
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public User()
        {
            CreatedAt = DateTimeOffset.Now;
            UpdatedAt = DateTimeOffset.Now; ;
        }

        //public ICollection<Log> Logs { get; set; }
    }
}
