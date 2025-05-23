using System.Windows;
using Agora.Rtc;
public class MyRtcEventHandler : IRtcEngineEventHandler
{
    private AgoraManager _agoraManager;

    public MyRtcEventHandler(AgoraManager manager)
    {
        _agoraManager = manager;
    }

    public override void OnUserJoined(RtcConnection connection, uint uid, int elapsed)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            MessageBox.Show($"🎉 Remote user joined! UID = {uid}");
        });
    }
}
