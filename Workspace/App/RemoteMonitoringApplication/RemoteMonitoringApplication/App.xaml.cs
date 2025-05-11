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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        await ConnectToServer();

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

