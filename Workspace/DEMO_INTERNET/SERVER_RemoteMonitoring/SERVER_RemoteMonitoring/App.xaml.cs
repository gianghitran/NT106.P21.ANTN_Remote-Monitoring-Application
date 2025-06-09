using SERVER_RemoteMonitoring.Data;
using SERVER_RemoteMonitoring.Server;
using SERVER_RemoteMonitoring.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SERVER_RemoteMonitoring
{
    /// <summary>  
    /// Interaction logic for App.xaml  
    /// </summary>  
    public partial class App : Application
    {
        static DatabaseService _databaseService;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Lấy đúng đường dẫn file database
            string dbPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RemoteMonitoring.db");
            var dbService = new DatabaseService(dbPath);

            // Tạo bảng RoomClient trên đúng file
            await dbService.EnsureRoomClientTableAsync();

            // Lấy port từ args nếu có
            int port = 8080;
            if (e.Args.Length > 0 && int.TryParse(e.Args[0], out int p))
                port = p;

            // Khởi động TCP server và truyền dbService
            await StartTCPServerAsync(port, dbService);

            // Khởi tạo UI server với database đã có
            var _server = new SERVER(dbService);

            _server.Show();
        }

        public static async Task<DatabaseService> GetDatabaseServiceAsync()
        {
            if (_databaseService == null)
            {
                string dbPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RemoteMonitoring.db");
                _databaseService = new DatabaseService(dbPath);
                await _databaseService.InitDatabase();
            }
            return _databaseService;
        }

        // Sửa lại StartTCPServerAsync để nhận dbService
        private async Task StartTCPServerAsync(int port, DatabaseService dbService)
        {
            var authService = new AuthService(dbService);
            var saveLogService = new SaveLogService(dbService);
            var _tcpServer = new TCPServer(authService, saveLogService, port, dbService);
            await Task.Run(() => _tcpServer.Start());
        }
    }
}
