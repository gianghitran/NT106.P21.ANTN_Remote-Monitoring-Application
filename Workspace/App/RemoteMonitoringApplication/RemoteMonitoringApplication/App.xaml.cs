using RemoteMonitoringApplication.Services;
using RemoteMonitoringApplication.Views;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Net.Sockets;

namespace RemoteMonitoringApplication;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{

    static ConnectServer _connect = null;
    static BroadcastLANServer _broadcastServer = new BroadcastLANServer();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string host = await _broadcastServer.DiscoverServer();

        _connect = new ConnectServer(host, 8001);

        await ConnectToServer();

        var _authService = new AuthService(_connect.GetClient());

        SessionManager.Instance.tcpClient = _connect.GetClient();

        var loginWindow = new Login(_authService);
        loginWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        // Clean up resources or perform any necessary actions before exiting
        _connect.DisconnectAsync().Wait();
        base.OnExit(e);
    }

    public async Task ConnectToServer()
    {
        await Task.Run(() => _connect.ConnectAsync());
    }
}

