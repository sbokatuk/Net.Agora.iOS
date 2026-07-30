using System.Text;
using Net.Agora.Video.iOS;

namespace Net.Agora.Sample.iOS;

/// <summary>
/// Joins an Agora RTC channel, publishing this device's camera/microphone and rendering the first
/// remote user's video — the same flow as Net.Agora/samples/Net.Agora.Sample, but built directly
/// against <see cref="AgoraRtcEngineKit"/> rather than the cross-platform façade, since that
/// façade lives in a separate repository this one does not depend on. Flip and Mute drive the
/// raw camera/mute surface; every call answers an Agora error code, 0 for success. Engine
/// callbacks are consumed as the binding's generated C# events (<c>engine.DidJoinedOfUid += …</c>)
/// rather than through a hand-written <c>AgoraRtcEngineDelegate</c> subclass — see the comment at
/// the subscription site for the one rule that choice comes with.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _status = new();

    private AgoraRtcEngineKit? _engine;
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

        // No delegate at construction: this sample consumes the engine's callbacks as C# events,
        // and events and a hand-assigned delegate are mutually exclusive — the first subscription
        // installs the binding's internal delegate, replacing whatever SharedEngine was given, and
        // assigning Delegate afterwards would silently detach every event. One style per engine
        // instance; the Voice sample shows the delegate-subclass style.
        var engine = AgoraRtcEngineKit.SharedEngine(config, null);

        // The SDK raises these on its own thread; Append dispatches itself, so only the handlers
        // that touch views hop to the main thread explicitly.
        engine.DidJoinChannel += (_, args) => Append($"joined {args.Channel} as {args.Uid}");
        engine.DidLeaveChannel += (_, _) => Append("left channel");
        engine.DidOccurError += (_, args) => Append($"error {args.ErrorCode}");

        engine.DidJoinedOfUid += (_, args) => MainThread.BeginInvokeOnMainThread(() =>
        {
            Append($"remote user joined: {args.Uid}");

            // Only one remote view in this sample — the first remote user gets it.
            if (_remoteUid is null && RemoteView.PlatformView is { } remoteView)
            {
                _remoteUid = args.Uid;
                _engine?.SetupRemoteVideo(new AgoraRtcVideoCanvas { Uid = args.Uid, View = remoteView });
            }
        });

        engine.DidOfflineOfUid += (_, args) => MainThread.BeginInvokeOnMainThread(() =>
        {
            Append($"remote user left: {args.Uid}");
            if (_remoteUid == args.Uid)
            {
                _remoteUid = null;
            }
        });

        engine.SetupLocalVideo(new AgoraRtcVideoCanvas { Uid = 0, View = localView });
        engine.EnableVideo();

        // Without this, EnableVideo alone never turns the camera on: JoinChannel does not imply
        // it, unlike what Agora's own docs suggest for the plain (no ChannelMediaOptions) join
        // overload used below. Missing this call is silent — the channel join itself still
        // succeeds, DidJoinChannel still fires — but no frames are ever captured, so both the
        // local view and whatever the remote side receives stay blank. See
        // Net.Agora.Sample.Android's OnStartPreviewClicked, which calls this unconditionally
        // before ever touching JoinChannel — this is the one call that sample has and this one
        // does not.
        engine.StartPreview();

        // This plain JoinChannel overload (no AgoraRtcChannelMediaOptions) defaults the client
        // role to Audience, not Broadcaster — confirmed via agoraapi.log's
        // setUserRoleStatus(role:2, …) on a join neither sample asked for that role on. Audience
        // neither publishes nor is notified of other Audience members joining, so two
        // Audience-role apps in the same channel each connect fine but never see or hear each
        // other — exactly the "each only sees myself" symptom this fixes. See
        // Net.Agora.Sample.Android's OnStartPreviewClicked for the same fix.
        engine.SetClientRole(AgoraClientRole.Broadcaster);

        var token = string.IsNullOrEmpty(TokenEntry.Text) ? null : TokenEntry.Text!.Trim();
        var joinResult = engine.JoinChannel(token, channelId, null, 0, joinSuccess: null);
        Append(joinResult == 0 ? $"joining {channelId}…" : $"JoinChannel returned {joinResult}");

        _engine = engine;
        SetJoined(true);
    }

    private void OnLeaveClicked(object? sender, EventArgs e)
    {
        _engine?.LeaveChannel(null);
        AgoraRtcEngineKit.Destroy();
        _engine = null;
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
        }
    }
}
