using SERVER_RemoteMonitoring.Data;
using SERVER_RemoteMonitoring.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

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
        public async Task<List<Models.Log>> GetLogsAsync()
        {
            try
            {
                var db = _dbService.GetDataBaseConnection();

                //sắp xếp theo thời gian giảm dần
                var logs = await db.Table<Models.Log>()
                                   .OrderByDescending(log => log.LogAt)
                                   .ToListAsync();

                return logs;
            }
            catch (Exception ex)
            {
                // TODO: Log lỗi 
                return new List<Models.Log>();
            }
        }
        public async Task LoadLogsToDataGrid(DataGrid DashboardDataGrid)
        {
            var logs = await GetLogsAsync();
            DashboardDataGrid.ItemsSource = logs;
        }
        public async Task<bool> ConnecionAsync(string username, string userId, string role, string partnerName, string partnerId)
        {
            try
            {
                var db = _dbService.GetDataBaseConnection();

                var newConnection = new Connections
                {
                    UserName = username,
                    UserId = userId,
                    Role = role,
                    PartnerName = partnerName,
                    PartnerId = partnerId,
                    ConnectAt = DateTime.UtcNow
                };

                await db.InsertAsync(newConnection);

                return true;
            }
            catch (Exception ex)
            {
                // TODO: Log lỗi 
                return false;
            }
                
        }
        public async Task<bool> UserLoginAsync(string username, string userId, string usersession)
        {
            try
            {
                var db = _dbService.GetDataBaseConnection();

                var newUserLogin = new UserLogin
                {
                    UserName = username,
                    UserId = userId,
                    UserSessionID = usersession,
                    ConnectAt = DateTime.UtcNow
                };

                await db.InsertAsync(newUserLogin);

                return true;
            }
            catch (Exception ex)
            {
                // TODO: Log lỗi 
                return false;
            }

        }


    }
}
