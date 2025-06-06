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

            //MessageBox.Show("Starting the server...");
            await StartTCPServerAsync();
            await GetDatabaseServiceAsync();
            //MessageBox.Show("Database initialized successfully.");
            // Initialize the server
            var _server = new SERVER(_databaseService);
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

        private async Task StartTCPServerAsync()
        {
            var db = await GetDatabaseServiceAsync();
            var authService = new AuthService(db);
            var saveLogService = new SaveLogService(db);
            var _tcpServer = new TCPServer(authService, saveLogService);
            await Task.Run(() => _tcpServer.Start());
        }
    }
}
