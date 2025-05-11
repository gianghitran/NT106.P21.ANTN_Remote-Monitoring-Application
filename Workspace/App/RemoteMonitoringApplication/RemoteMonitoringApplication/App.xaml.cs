using RemoteMonitoringApplication.Services;
using RemoteMonitoringApplication.Views;
using System.Configuration;
using System.Data;
using System.Windows;

namespace RemoteMonitoringApplication;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    static WebSocketConnectServer _connect = new WebSocketConnectServer("ws://localhost:8080");

    public class RegisterMessage
    {
        public string command { get; set; }
        public string username { get; set; }
        public string email { get; set; }
        public string password { get; set; }
    }

    public class LoginMessage
    {
        public string command { get; set; }
        public string username { get; set; }
        public string password { get; set; }
    }
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        await ConnectToServer();

        // Check if register is working

        // Check if login is working


        var loginWindow = new Login();
        loginWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        // Clean up resources or perform any necessary actions before exiting
        base.OnExit(e);
    }

    public async Task ConnectToServer()
    {
        await Task.Run(() => _connect.ConnectAsync());
    }
}

