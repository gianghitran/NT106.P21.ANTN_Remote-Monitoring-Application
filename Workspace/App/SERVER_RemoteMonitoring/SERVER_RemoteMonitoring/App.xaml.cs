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

            int port = 8080;
            if (e.Args.Length > 0 && int.TryParse(e.Args[0], out int p))
                port = p;

            await StartTCPServerAsync(port, dbService); // Truyền dbService này vào
            var _server = new SERVER();
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
            var _tcpServer = new TCPServer(authService, port, dbService);
            await Task.Run(() => _tcpServer.Start());
        }
    }
}
