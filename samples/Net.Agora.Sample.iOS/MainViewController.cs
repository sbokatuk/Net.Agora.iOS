using AVFoundation;
using Net.Agora.Video.iOS;
using UIKit;

namespace Net.Agora.Sample.iOS;

/// <summary>
/// Joins an Agora RTC channel, publishing this device's camera/microphone and rendering the first
/// remote user's video — the same flow as Net.Agora/samples/Net.Agora.Sample, but built directly
/// against <see cref="AgoraRtcEngineKit"/> rather than the cross-platform façade, since that
/// façade lives in a separate repository this one does not depend on.
/// </summary>
public sealed class MainViewController : UIViewController
{
    private readonly UITextField _appIdField = new() { Placeholder = "Agora App ID", BorderStyle = UITextBorderStyle.RoundedRect };
    private readonly UITextField _channelIdField = new() { Placeholder = "channel id", BorderStyle = UITextBorderStyle.RoundedRect };
    private readonly UITextField _tokenField = new() { Placeholder = "token (optional — App ID-only auth is testing-only)", BorderStyle = UITextBorderStyle.RoundedRect };

    // Both views exist for the whole session: creating one on demand would mean creating a native
    // renderer mid-layout, which the SDK does not expect — see the local/remote view comment in
    // the façade repo's AgoraVideoView.
    private readonly UIView _localView = new() { BackgroundColor = UIColor.Black };
    private readonly UIView _remoteView = new() { BackgroundColor = UIColor.Black };

    private readonly UIButton _joinButton = UIButton.FromType(UIButtonType.System);
    private readonly UIButton _leaveButton = UIButton.FromType(UIButtonType.System);
    private readonly UITextView _statusView = new() { Editable = false, Font = UIFont.SystemFontOfSize(12) };

    private AgoraRtcEngineKit? _engine;
    private EngineDelegate? _delegate;
    private nuint? _remoteUid;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        Title = "Net.Agora.Video.iOS Sample";
        View!.BackgroundColor = UIColor.SystemBackground;

        _joinButton.SetTitle("Join", UIControlState.Normal);
        _joinButton.TouchUpInside += OnJoinTapped;
        _leaveButton.SetTitle("Leave", UIControlState.Normal);
        _leaveButton.TouchUpInside += OnLeaveTapped;
        _leaveButton.Enabled = false;

        BuildLayout();
    }

    private void BuildLayout()
    {
        var fieldsStack = new UIStackView([_appIdField, _channelIdField, _tokenField])
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 8,
        };

        var videoStack = new UIStackView([_localView, _remoteView])
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Distribution = UIStackViewDistribution.FillEqually,
            Spacing = 8,
        };

        var buttonStack = new UIStackView([_joinButton, _leaveButton])
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Spacing = 16,
        };

        var rootStack = new UIStackView([fieldsStack, videoStack, buttonStack, _statusView])
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 12,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };

        View!.AddSubview(rootStack);

        var guide = View.SafeAreaLayoutGuide;
        NSLayoutConstraint.ActivateConstraints(
        [
            rootStack.TopAnchor.ConstraintEqualTo(guide.TopAnchor, 16),
            rootStack.LeadingAnchor.ConstraintEqualTo(guide.LeadingAnchor, 16),
            rootStack.TrailingAnchor.ConstraintEqualTo(guide.TrailingAnchor, -16),
            rootStack.BottomAnchor.ConstraintEqualTo(guide.BottomAnchor, -16),
            videoStack.HeightAnchor.ConstraintEqualTo(240),
            _statusView.HeightAnchor.ConstraintEqualTo(120),
        ]);
    }

    private async void OnJoinTapped(object? sender, EventArgs e)
    {
        var appId = _appIdField.Text?.Trim();
        var channelId = _channelIdField.Text?.Trim();

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

        // Created here rather than eagerly: SetupLocalVideo/SetupRemoteVideo add a subview to
        // whatever UIView is passed, which needs a window to render into — this view controller
        // is on screen by the time Join is tapped.
        var config = new AgoraRtcEngineConfig { AppId = appId };
        _delegate = new EngineDelegate(this);
        var engine = AgoraRtcEngineKit.SharedEngine(config, _delegate);

        engine.SetupLocalVideo(new AgoraRtcVideoCanvas { Uid = 0, View = _localView });
        engine.EnableVideo();

        var token = string.IsNullOrEmpty(_tokenField.Text) ? null : _tokenField.Text!.Trim();
        engine.JoinChannel(token, channelId, null, 0, joinSuccess: null);

        _engine = engine;
        SetJoined(true);
    }

    private void OnLeaveTapped(object? sender, EventArgs e)
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
        var camera = await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVAuthorizationMediaType.Video);
        var microphone = await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVAuthorizationMediaType.Audio);
        return camera && microphone;
    }

    private void SetJoined(bool joined)
    {
        _joinButton.Enabled = !joined;
        _leaveButton.Enabled = joined;
    }

    private void Append(string message) => InvokeOnMainThread(() =>
        _statusView.Text += $"{DateTime.Now:HH:mm:ss}  {message}\n");

    public override void ViewWillDisappear(bool animated)
    {
        base.ViewWillDisappear(animated);

        if (IsMovingFromParentViewController || IsBeingDismissed)
        {
            _engine?.LeaveChannel(null);
            AgoraRtcEngineKit.Destroy();
        }
    }

    /// <summary>
    /// Translates <c>AgoraRtcEngineDelegate</c>'s (all-optional) callbacks into UI updates. join/
    /// leave success is also reported through the block parameters on JoinChannel/LeaveChannel,
    /// but the delegate is used uniformly here so both entry points (the initial join and a
    /// server-initiated rejoin) go through the same path — same reasoning as the façade repo's
    /// Platforms/Apple/AgoraVideoClient.cs.
    /// </summary>
    private sealed class EngineDelegate(MainViewController owner) : AgoraRtcEngineDelegate
    {
        public override void DidJoinChannel(AgoraRtcEngineKit engine, string channel, nuint uid, nint elapsed) =>
            owner.Append($"joined {channel} as {uid}");

        public override void DidLeaveChannel(AgoraRtcEngineKit engine, AgoraChannelStats stats) =>
            owner.Append("left channel");

        public override void DidJoinedOfUid(AgoraRtcEngineKit engine, nuint uid, nint elapsed) => owner.InvokeOnMainThread(() =>
        {
            owner.Append($"remote user joined: {uid}");

            // Only one remote view in this sample — the first remote user gets it.
            if (owner._remoteUid is null)
            {
                owner._remoteUid = uid;
                owner._engine?.SetupRemoteVideo(new AgoraRtcVideoCanvas { Uid = uid, View = owner._remoteView });
            }
        });

        public override void DidOfflineOfUid(AgoraRtcEngineKit engine, nuint uid, AgoraUserOfflineReason reason) => owner.InvokeOnMainThread(() =>
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
