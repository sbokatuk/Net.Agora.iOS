using System.Text;
using Net.Agora.Voice.iOS;

namespace Net.Agora.Sample.Voice.iOS;

/// <summary>
/// Joins an Agora RTC channel audio-only — the same flow as
/// Net.Agora/samples/Net.Agora.Sample.Voice, but built directly against
/// <see cref="AgoraRtcEngineKit"/> from the Voice binding rather than the cross-platform façade,
/// since that façade lives in a separate repository this one does not depend on. Every raw call
/// answers an Agora error code, 0 for success — the façade hides this.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _status = new();

    private AgoraRtcEngineKit? _engine;
    private EngineDelegate? _delegate;

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

        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
        {
            Append("microphone permission denied");
            return;
        }

        var config = new AgoraRtcEngineConfig { AppId = appId };
        _delegate = new EngineDelegate(this);
        var engine = AgoraRtcEngineKit.SharedEngine(config, _delegate);

        engine.EnableAudio();

        // Before the join: the default route. The live override (SetEnableSpeakerphone) answers
        // -3 until an audio session exists — see the binding's comments.
        engine.SetDefaultAudioRouteToSpeakerphone(SpeakerSwitch.IsToggled);

        // Who-is-speaking reports every 200 ms — drives the label above; uid 0 is this device.
        engine.EnableAudioVolumeIndication(200, 3, reportVad: false);

        // Without this the plain JoinChannel overload below joins as Audience, which publishes
        // nothing and isn't told about other Audience members: two of these samples would each sit
        // in the channel alone, silent. See Net.Agora.Sample.iOS for the same fix.
        engine.SetClientRole(AgoraClientRole.Broadcaster);

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
        SetJoined(false);
        Append("left");
    }

    private void OnMuteToggled(object? sender, ToggledEventArgs e)
    {
        var code = _engine?.MuteLocalAudioStream(e.Value) ?? -1;
        Append(code == 0
            ? (e.Value ? "microphone muted" : "microphone unmuted")
            : $"muteLocalAudioStream returned {code}");
    }

    private void OnSpeakerToggled(object? sender, ToggledEventArgs e)
    {
        // Live in a channel the override applies; before one, set the default route instead.
        var code = _engine is null
            ? -1
            : (LeaveButton.IsEnabled
                ? _engine.SetEnableSpeakerphone(e.Value)
                : _engine.SetDefaultAudioRouteToSpeakerphone(e.Value));
        Append(code == 0
            ? (e.Value ? "audio routed to speaker" : "audio routed to earpiece")
            : $"speaker route returned {code}");
    }

    private void SetJoined(bool joined)
    {
        JoinButton.IsEnabled = !joined;
        LeaveButton.IsEnabled = joined;
        MuteSwitch.IsEnabled = joined;
        if (!joined)
        {
            MuteSwitch.IsToggled = false;
            SpeakersLabel.Text = "nobody is speaking";
        }
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

        if (Handler is null && _engine is not null)
        {
            _engine.LeaveChannel(null);
            AgoraRtcEngineKit.Destroy();
            _engine = null;
            _delegate = null;
        }
    }

    /// <summary>
    /// Translates <c>AgoraRtcEngineDelegate</c>'s (all-optional) callbacks into UI updates. The
    /// SDK raises these on its own thread, so everything hops back to the main thread.
    /// </summary>
    private sealed class EngineDelegate(MainPage owner) : AgoraRtcEngineDelegate
    {
        public override void DidJoinChannel(AgoraRtcEngineKit engine, string channel, nuint uid, nint elapsed) =>
            owner.Append($"joined {channel} as {uid}");

        public override void DidLeaveChannel(AgoraRtcEngineKit engine, AgoraChannelStats stats) =>
            owner.Append("left channel");

        public override void DidJoinedOfUid(AgoraRtcEngineKit engine, nuint uid, nint elapsed) =>
            owner.Append($"remote user joined: {uid}");

        public override void DidOfflineOfUid(AgoraRtcEngineKit engine, nuint uid, AgoraUserOfflineReason reason) =>
            owner.Append($"remote user left: {uid}");

        public override void DidAudioMuted(AgoraRtcEngineKit engine, bool muted, nuint uid) =>
            owner.Append($"user {uid} {(muted ? "muted" : "unmuted")}");

        public override void ReportAudioVolumeIndication(
            AgoraRtcEngineKit engine, AgoraRtcAudioVolumeInfo[] speakers, nint totalVolume) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var speaking = speakers
                    .Where(s => s.Volume > 0)
                    .Select(s => s.Uid == 0 ? $"you ({s.Volume})" : $"{s.Uid} ({s.Volume})")
                    .ToList();
                owner.SpeakersLabel.Text = speaking.Count > 0
                    ? $"speaking: {string.Join(", ", speaking)}"
                    : "nobody is speaking";
            });

        public override void DidOccurError(AgoraRtcEngineKit engine, AgoraErrorCode errorCode) =>
            owner.Append($"error {errorCode}");
    }
}
