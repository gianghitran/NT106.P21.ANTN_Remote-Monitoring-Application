using System;
using System.Windows;
using System.Windows.Controls;
using Agora.Rtc;

public class AgoraManager
{
    private IRtcEngine _rtcEngine;
    private const string APP_ID = "0db5a66ce5354031b5346c6423930160"; // 🔁 THAY bằng App ID thật của bạn
    private string currentChannel = "";
    private MyRtcEventHandler _eventHandler;

    public void Initialize(Action<uint> onRemoteUserJoined, Action<uint> onRemoteUserLeft)
    {
        _rtcEngine = Agora.Rtc.RtcEngine.CreateAgoraRtcEngine();
        var context = new RtcEngineContext { appId = APP_ID };
        _rtcEngine.Initialize(context);
        _rtcEngine.EnableVideo(); // BỔ SUNG DÒNG NÀY
        _rtcEngine.InitEventHandler(new MyRtcEventHandler(this));

    }

    public async Task JoinChannel(string channelName, bool isScreenSharer)
    {
        currentChannel = channelName;

        int result = _rtcEngine.JoinChannel("", channelName, "", 0);
        MessageBox.Show("Join result: " + result);
        if (result != 0)
        {
            MessageBox.Show($"❌ JoinChannel failed: {result}");
            return;
        }

        // Đảm bảo không bật camera
        _rtcEngine.EnableLocalVideo(false);

        if (isScreenSharer)
        {
            _rtcEngine.SetClientRole(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);
        }
        else
        {
            _rtcEngine.SetClientRole(CLIENT_ROLE_TYPE.CLIENT_ROLE_AUDIENCE);
        }

        var options = new ChannelMediaOptions();
        options.publishCameraTrack.SetValue(false);
        options.publishMicrophoneTrack.SetValue(false);
        options.publishScreenTrack.SetValue(isScreenSharer);
        options.autoSubscribeAudio.SetValue(false);
        options.autoSubscribeVideo.SetValue(!isScreenSharer); // viewer = true
        options.clientRoleType.SetValue(isScreenSharer
            ? CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER
            : CLIENT_ROLE_TYPE.CLIENT_ROLE_AUDIENCE);
        options.channelProfile.SetValue(CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_LIVE_BROADCASTING);


        _rtcEngine.UpdateChannelMediaOptions(options);
    }

    public void StartScreenShare()
    {
        var screenCaptureParams = new ScreenCaptureParameters
        {
            frameRate = 15,
            bitrate = 0,
            captureMouseCursor = true
        };

        uint displayId = 0;

        int result = _rtcEngine.StartScreenCaptureByDisplayId(
            displayId,
            new Agora.Rtc.Rectangle(0, 0, 0, 0),
            screenCaptureParams
        );

        if (result != 0)
        {
            MessageBox.Show($"❌ StartScreenCaptureByDisplayId failed: {result}");
            return;
        }
        else
        {
            MessageBox.Show("✅ Đã bắt đầu chia sẻ màn hình");
        }

        // BỔ SUNG ĐOẠN NÀY
        var options = new ChannelMediaOptions();
        options.publishCameraTrack.SetValue(false);
        options.publishMicrophoneTrack.SetValue(false);
        options.publishScreenTrack.SetValue(true);
        options.autoSubscribeVideo.SetValue(false);
        options.autoSubscribeAudio.SetValue(false);
        _rtcEngine.UpdateChannelMediaOptions(options);
    }

    public void StopScreenShare()
    {
        _rtcEngine.StopScreenCapture();

        // Unpublish screen track, (có thể publish lại camera/mic nếu muốn)
        var options = new ChannelMediaOptions();
        options.publishScreenTrack.SetValue(false);
        options.publishCameraTrack.SetValue(false); // hoặc true nếu muốn bật lại camera
        options.publishMicrophoneTrack.SetValue(false); // hoặc true nếu muốn bật lại mic
        _rtcEngine.UpdateChannelMediaOptions(options);

        _rtcEngine.LeaveChannel();
    }

    public void RegisterVideoFrameObserver(IVideoFrameObserver observer)
    {
        _rtcEngine.RegisterVideoFrameObserver(observer);
    }

    public void SetupRemoteVideo(uint uid, IntPtr hwnd)
    {
        var canvas = new VideoCanvas
        {
            view = hwnd,
            uid = uid,
            renderMode = RENDER_MODE_TYPE.RENDER_MODE_FIT
        };
        _rtcEngine.SetupRemoteVideo(canvas);
    }

}
