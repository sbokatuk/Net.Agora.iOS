using System.Text;
using Net.Agora.Signaling.iOS;
using Net.Agora.Video.iOS;

namespace Net.Agora.Sample.iOS;

/// <summary>
/// Joins an Agora RTC channel, publishing this device's camera/microphone and rendering the first
/// remote user's video — the same flow as Net.Agora/samples/Net.Agora.Sample, but built directly
/// against <see cref="AgoraRtcEngineKit"/> rather than the cross-platform façade, since that
/// façade lives in a separate repository this one does not depend on. Flip and Mute drive the
/// raw camera/mute surface; every call answers an Agora error code, 0 for success.
///
/// The Signaling button drives <see cref="AgoraRtmClientKit"/> from the same app: the two
/// products coexist (different frameworks, different classes), which this sample proves at
/// bundle time just by building.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _status = new();

    private AgoraRtcEngineKit? _engine;
    private EngineDelegate? _delegate;
    private nuint? _remoteUid;
    private bool _muted;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnJoinClicked(object? sender, EventArgs e)
    {
        var appId = AppIdEntry.Text?.Trim();
        var channelId = ChannelIdEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(channelId))
        {
            Append("enter an App ID and a channel id first");
            return;
        }

        if (!await RequestCapturePermissionsAsync())
        {
            Append("camera and microphone permission denied");
            return;
        }

        // The native view exists once the page is on screen, which it is by the time Join can be
        // tapped — SetupLocalVideo adds a subview to whatever UIView is passed, which needs a
        // window to render into.
        var localView = LocalView.PlatformView;
        if (localView is null)
        {
            Append("the local video view has no native view yet");
            return;
        }

        var config = new AgoraRtcEngineConfig { AppId = appId };
        _delegate = new EngineDelegate(this);
        var engine = AgoraRtcEngineKit.SharedEngine(config, _delegate);

        engine.SetupLocalVideo(new AgoraRtcVideoCanvas { Uid = 0, View = localView });
        engine.EnableVideo();

        var token = string.IsNullOrEmpty(TokenEntry.Text) ? null : TokenEntry.Text!.Trim();
        engine.JoinChannel(token, channelId, null, 0, joinSuccess: null);

        _engine = engine;
        SetJoined(true);
    }

    private void OnLeaveClicked(object? sender, EventArgs e)
    {
        _engine?.LeaveChannel(null);
        AgoraRtcEngineKit.Destroy();
        _engine = null;
        _delegate = null;
        _remoteUid = null;
        SetJoined(false);
        Append("left");
    }

    private static async Task<bool> RequestCapturePermissionsAsync()
    {
        var camera = await Permissions.RequestAsync<Permissions.Camera>();
        var microphone = await Permissions.RequestAsync<Permissions.Microphone>();

        return camera == PermissionStatus.Granted && microphone == PermissionStatus.Granted;
    }

    private void SetJoined(bool joined)
    {
        JoinButton.IsEnabled = !joined;
        LeaveButton.IsEnabled = joined;
        FlipButton.IsEnabled = joined;
        MuteButton.IsEnabled = joined;
        if (!joined)
        {
            _muted = false;
            MuteButton.Text = "Mute";
        }
    }

    private void OnFlipClicked(object? sender, EventArgs e)
    {
        var code = _engine?.SwitchCamera() ?? -1;
        Append(code == 0 ? "camera switched" : $"switchCamera returned {code}");
    }

    private void OnMuteClicked(object? sender, EventArgs e)
    {
        _muted = !_muted;
        var code = _engine?.MuteLocalAudioStream(_muted) ?? -1;
        MuteButton.Text = _muted ? "Unmute" : "Mute";
        Append(code == 0
            ? (_muted ? "microphone muted" : "microphone unmuted")
            : $"muteLocalAudioStream returned {code}");
    }

    private void OnSignalingClicked(object? sender, EventArgs e)
    {
        var appId = AppIdEntry.Text?.Trim();
        if (string.IsNullOrEmpty(appId))
        {
            Append("enter an App ID first");
            return;
        }

        // The raw RTM surface, from the same process the RTC engine runs in. Login is a live
        // signalling call reported through a completion block; with an unregistered App ID it
        // answers an error, which is exactly what this demo shows — the round trip, credentials
        // or not. The façade's Net.Agora.Signaling wraps this into awaitable calls.
        var config = new AgoraRtmClientConfig(appId, "net-agora-sample");
        var rtm = new AgoraRtmClientKit(config, del: null, out var error);
        if (rtm.Handle == ObjCRuntime.NativeHandle.Zero)
        {
            Append($"RTM rejected the configuration: {error?.LocalizedDescription}");
            return;
        }

        Append("RTM created — logging in…");
        rtm.Login(appId, (response, errorInfo) => MainThread.BeginInvokeOnMainThread(() =>
        {
            Append(errorInfo is null || errorInfo.ErrorCode == 0
                ? "RTM login succeeded"
                : $"RTM login answered: {errorInfo.Reason}");
            rtm.Destroy();
        }));
    }

    private void Append(string message) => MainThread.BeginInvokeOnMainThread(() =>
    {
        _status.AppendLine($"{DateTime.Now:HH:mm:ss}  {message}");
        StatusLabel.Text = _status.ToString();
        StatusScroll.ScrollToAsync(0, StatusLabel.Height, animated: false);
    });

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        // Releases the camera and the native renderers when the page goes away; the engine holds
        // native resources that managed collection alone does not reclaim.
        if (Handler is null && _engine is not null)
        {
            _engine.LeaveChannel(null);
            AgoraRtcEngineKit.Destroy();
            _engine = null;
            _delegate = null;
        }
    }

    /// <summary>
    /// Translates <c>AgoraRtcEngineDelegate</c>'s (all-optional) callbacks into UI updates. join/
    /// leave success is also reported through the block parameters on JoinChannel/LeaveChannel,
    /// but the delegate is used uniformly here so both entry points (the initial join and a
    /// server-initiated rejoin) go through the same path — same reasoning as the façade repo's
    /// Platforms/Apple/AgoraVideoClient.cs.
    /// </summary>
    private sealed class EngineDelegate(MainPage owner) : AgoraRtcEngineDelegate
    {
        public override void DidJoinChannel(AgoraRtcEngineKit engine, string channel, nuint uid, nint elapsed) =>
            owner.Append($"joined {channel} as {uid}");

        public override void DidLeaveChannel(AgoraRtcEngineKit engine, AgoraChannelStats stats) =>
            owner.Append("left channel");

        public override void DidJoinedOfUid(AgoraRtcEngineKit engine, nuint uid, nint elapsed) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                owner.Append($"remote user joined: {uid}");

                // Only one remote view in this sample — the first remote user gets it.
                if (owner._remoteUid is null && owner.RemoteView.PlatformView is { } remoteView)
                {
                    owner._remoteUid = uid;
                    owner._engine?.SetupRemoteVideo(new AgoraRtcVideoCanvas { Uid = uid, View = remoteView });
                }
            });

        public override void DidOfflineOfUid(AgoraRtcEngineKit engine, nuint uid, AgoraUserOfflineReason reason) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                owner.Append($"remote user left: {uid}");
                if (owner._remoteUid == uid)
                {
                    owner._remoteUid = null;
                }
            });

        public override void DidOccurError(AgoraRtcEngineKit engine, AgoraErrorCode errorCode) =>
            owner.Append($"error {errorCode}");
    }
}
