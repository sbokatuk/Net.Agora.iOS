using System.Text;
using Net.Agora.Signaling.iOS;

namespace Net.Agora.Sample.Signaling.iOS;

/// <summary>
/// A tiny chat room over Agora Signaling, driven through the <b>raw</b>
/// <c>Net.Agora.Signaling.iOS</c> binding rather than the cross-platform façade — the point of
/// this sample is to show what <c>Net.Agora.Signaling</c> hides.
///
/// Every operation here takes an Objective-C completion block (login, subscribe, publish), and
/// events arrive on an <c>AgoraRtmClientDelegate</c>. The façade in <c>sbokatuk/Net.Agora</c>
/// turns all of that into awaitable calls and ordinary .NET events; if that is what you want, use
/// <c>Net.Agora.Signaling</c> instead, which <c>samples/Net.Agora.Sample.Signaling</c> there
/// demonstrates.
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

    private void OnLoginClicked(object sender, EventArgs e)
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

        // An App ID-only project logs in with the App ID as the token.
        _client.Login(appId, (response, errorInfo) =>
        {
            if (errorInfo is not null && errorInfo.ErrorCode != 0)
            {
                Append($"login failed: {errorInfo.Reason}");
                MainThread.BeginInvokeOnMainThread(() => LoginButton.IsEnabled = true);
                return;
            }

            Subscribe(channel);
        });
    }

    private void Subscribe(string channel) =>
        _client?.Subscribe(channel, option: null, (response, errorInfo) =>
        {
            if (errorInfo is not null && errorInfo.ErrorCode != 0)
            {
                Append($"subscribe failed: {errorInfo.Reason}");
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Append($"logged in and subscribed to {channel}");
                SetLoggedIn(true);
            });
        });

    private void OnLogoutClicked(object sender, EventArgs e)
    {
        if (_client is not null && _channel is not null)
        {
            _client.Unsubscribe(_channel, (response, errorInfo) =>
                Append(errorInfo is { ErrorCode: not 0 }
                    ? $"unsubscribe failed: {errorInfo.Reason}"
                    : $"unsubscribed from {_channel}"));
        }

        _client?.Logout(null);
        _client?.Destroy();
        _client = null;
        _channel = null;
        Append("logged out");
        SetLoggedIn(false);
    }

    private void OnSendClicked(object sender, EventArgs e)
    {
        var message = MessageEntry.Text;
        if (_client is null || _channel is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _client.Publish(_channel, message, option: null, (response, errorInfo) =>
        {
            if (errorInfo is not null && errorInfo.ErrorCode != 0)
            {
                Append($"send failed: {errorInfo.Reason}");
                return;
            }

            // A publisher does not receive its own message back — echo it locally.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Append($"you: {message}");
                MessageEntry.Text = "";
            });
        });
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
