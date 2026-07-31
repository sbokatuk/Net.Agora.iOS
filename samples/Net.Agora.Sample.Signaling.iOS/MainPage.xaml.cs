using System.Text;
using Net.Agora.Signaling.iOS;

namespace Net.Agora.Sample.Signaling.iOS;

/// <summary>
/// A tiny chat room over Agora Signaling, driven through the <b>raw</b>
/// <c>Net.Agora.Signaling.iOS</c> binding rather than the cross-platform façade — the point of
/// this sample is to show what <c>Net.Agora.Signaling</c> hides.
///
/// The binding itself is awaitable now: every completion-block method carries an [Async]
/// counterpart (<c>LoginAsync</c>, <c>SubscribeAsync</c>, <c>PublishAsync</c>, …), so this page
/// awaits them in a straight line instead of nesting completion blocks. Each Task completes —
/// rather than faults — with the (Response, ErrorInfo) pair, because the SDK answers some
/// successes with a non-nil errorInfo whose code is 0; success is therefore checked as
/// <c>result.ErrorInfo?.ErrorCode</c>. Events still arrive on an <c>AgoraRtmClientDelegate</c>.
/// The façade in <c>sbokatuk/Net.Agora</c> layers ordinary .NET events over those too.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _log = new();
    private AgoraRtmClientKit? _client;
    private Delegate? _delegate;
    private string? _channel;

    public MainPage()
    {
        InitializeComponent();
    }

    // The SDK fires its completion blocks off the main thread, but awaiting from a UI event
    // handler captures MAUI's SynchronizationContext, so everything after each await below is
    // back on the main thread — which is why, unlike the completion-block version of this page,
    // no callback needs a MainThread.BeginInvokeOnMainThread hop of its own.
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var appId = AppIdEntry.Text?.Trim();
        var userId = UserIdEntry.Text?.Trim();
        var channel = ChannelEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(channel))
        {
            Append("enter an App ID, a user id and a channel first");
            return;
        }

        LoginButton.IsEnabled = false;

        _delegate = new Delegate(this);
        var config = new AgoraRtmClientConfig(appId, userId);

        // initWithConfig:delegate:error: returns nil with the error populated for a config the SDK
        // rejects outright.
        _client = new AgoraRtmClientKit(config, _delegate, out var error);
        if (_client.Handle == ObjCRuntime.NativeHandle.Zero)
        {
            Append($"could not create client: {error?.LocalizedDescription}");
            LoginButton.IsEnabled = true;
            return;
        }

        _channel = channel;

        // Falls back to the App ID as the token when the field is left blank — an
        // App-Certificate-less (testing) project accepts that; a real project rejects it with
        // "Invalid token", which is exactly what a real RTM token (TokenEntry) is for.
        var token = TokenEntry.Text?.Trim();
        var login = await _client.LoginAsync(string.IsNullOrEmpty(token) ? appId : token);
        if (login.ErrorInfo is { ErrorCode: not 0 })
        {
            Append($"login failed: {login.ErrorInfo.Reason}");
            LoginButton.IsEnabled = true;
            return;
        }

        var subscribe = await _client.SubscribeAsync(channel, option: null);
        if (subscribe.ErrorInfo is { ErrorCode: not 0 })
        {
            Append($"subscribe failed: {subscribe.ErrorInfo.Reason}");
            return;
        }

        Append($"logged in and subscribed to {channel}");
        SetLoggedIn(true);
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        if (_client is not null)
        {
            if (_channel is not null)
            {
                var unsubscribe = await _client.UnsubscribeAsync(_channel);
                Append(unsubscribe.ErrorInfo is { ErrorCode: not 0 }
                    ? $"unsubscribe failed: {unsubscribe.ErrorInfo.Reason}"
                    : $"unsubscribed from {_channel}");
            }

            // Awaited so Destroy provably runs after the logout finished — the completion-block
            // version fired Logout and destroyed the client in the same breath.
            await _client.LogoutAsync();
            _client.Destroy();
        }

        _client = null;
        _channel = null;
        Append("logged out");
        SetLoggedIn(false);
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        var message = MessageEntry.Text;
        if (_client is null || _channel is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var publish = await _client.PublishAsync(_channel, message, option: null);
        if (publish.ErrorInfo is { ErrorCode: not 0 })
        {
            Append($"send failed: {publish.ErrorInfo.Reason}");
            return;
        }

        // A publisher does not receive its own message back — echo it locally.
        Append($"you: {message}");
        MessageEntry.Text = "";
    }

    private void SetLoggedIn(bool loggedIn)
    {
        LoginButton.IsEnabled = !loggedIn;
        LogoutButton.IsEnabled = loggedIn;
        MessageEntry.IsEnabled = loggedIn;
        SendButton.IsEnabled = loggedIn;
    }

    private void Append(string message) => MainThread.BeginInvokeOnMainThread(() =>
    {
        _log.AppendLine($"{DateTime.Now:HH:mm:ss}  {message}");
        MessagesLabel.Text = _log.ToString();
        MessagesScroll.ScrollToAsync(0, MessagesLabel.Height, animated: false);
    });

    /// <summary>The all-optional delegate the SDK calls back on — what the façade turns into events.</summary>
    private sealed class Delegate(MainPage owner) : AgoraRtmClientDelegate
    {
        public override void DidReceiveMessageEvent(AgoraRtmClientKit rtmKit, AgoraRtmMessageEvent @event)
        {
            var text = @event.Message?.StringData ?? "[binary]";
            owner.Append($"{@event.Publisher}: {text}");
        }

        public override void ConnectionChangedToState(
            AgoraRtmClientKit rtmKit, string channelName, AgoraRtmClientConnectionState state, nint reason) =>
            owner.Append($"connection: {state}");

        public override void TokenPrivilegeWillExpire(AgoraRtmClientKit rtmKit, string? channel) =>
            owner.Append("token expires soon — renew it");
    }
}
