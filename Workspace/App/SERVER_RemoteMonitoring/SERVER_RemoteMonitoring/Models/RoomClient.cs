using SQLite;

namespace SERVER_RemoteMonitoring.Models
{
    public class RoomClient
    {
        [PrimaryKey]
        public string Id { get; set; }
        public string Password { get; set; }
        public int ServerPort { get; set; } 
    }
}