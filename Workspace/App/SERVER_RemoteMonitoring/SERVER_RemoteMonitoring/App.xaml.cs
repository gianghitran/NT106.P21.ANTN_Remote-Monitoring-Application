using SERVER_RemoteMonitoring.Data;
using SERVER_RemoteMonitoring.Server;
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

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);


            MessageBox.Show("Starting the server...");
            await GetDatabaseServiceAsync();
            MessageBox.Show("Database initialized successfully.");
            // Initialize the server
            var _server = new SERVER();
            _server.Show();
        }

    }
}
