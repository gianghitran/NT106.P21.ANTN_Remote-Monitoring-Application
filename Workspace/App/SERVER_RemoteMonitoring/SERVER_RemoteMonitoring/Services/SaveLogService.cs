using SERVER_RemoteMonitoring.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SERVER_RemoteMonitoring.Services
{
    public class SaveLogService
    {
        private readonly DatabaseService _dbService;

        public SaveLogService(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public async Task<bool> LogAsync(string username, string role, string partner, string action)
        {
            try
            {
                var db = _dbService.GetDataBaseConnection();

                var newLog = new Models.Log
                {
                    UserId = username,
                    Role = role,
                    PartnerId = partner,
                    Action = action,
                    LogAt = DateTime.UtcNow
                };

                await db.InsertAsync(newLog);

                return true;
            }
            catch (Exception ex)
            {
                // TODO: Log lỗi nếu cần
                return false;
            }
        }
    }
}
