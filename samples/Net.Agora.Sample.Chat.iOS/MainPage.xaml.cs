using System.Text;
using Net.Agora.Chat.iOS;

namespace Net.Agora.Sample.Chat.iOS;

/// <summary>
/// A tiny two-user chat over Agora Chat, driven through the <b>raw</b> <c>Net.Agora.Chat.iOS</c>
/// binding rather than the cross-platform façade — the point of this sample is to show what
/// <c>Net.Agora.Chat</c> hides.
///
/// <see cref="AgoraChatClient"/> is a singleton (<c>SharedClient</c>): initialise it once with
/// <see cref="AgoraChatOptions"/>, then every completion-block method carries an [Async]
/// counterpart (<c>LoginAsync</c>, <c>SendMessageAsync</c>, …). Those Tasks complete — never
/// fault — because <c>AgoraChatError</c> is not an NSError; a null error means success. Incoming
/// messages arrive on <see cref="AgoraChatManagerDelegate"/>, added to the manager the singleton
/// hands out after login. The façade in <c>sbokatuk/Net.Agora</c> (package <c>Net.Agora.Chat</c>)
/// layers ordinary .NET events over both delegates.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _log = new();
    private ClientDelegate? _clientDelegate;
    private ManagerDelegate? _managerDelegate;
    private string? _peer;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var appKey = AppKeyEntry.Text?.Trim();
        var username = UsernameEntry.Text?.Trim();
        var token = TokenEntry.Text?.Trim();
        var peer = PeerEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(peer))
        {
            Append("enter an App Key, a username, a user token and a peer username first");
            return;
        }

        LoginButton.IsEnabled = false;

        var client = AgoraChatClient.SharedClient;

        // Synchronous, and returns nil on success — the SDK's convention for its non-block calls.
        // Harmless to call again on a client that is already initialised, which matters here since
        // SharedClient is a process-wide singleton and this page may run its login flow more than
        // once.
        //
        // Accepts either shape the Agora Console hands out: the legacy "org#appName" Chat App Key,
        // or the plain Agora App ID newer Chat projects are provisioned with — distinguished by the
        // "#" only the App Key form contains. FromAppKey on a bare App ID fails native-side
        // parameter validation (AgoraChatError "Param is error"), since the SDK expects the
        // "org#appName" shape specifically for that factory method.
        var options = appKey.Contains('#')
            ? AgoraChatOptions.FromAppKey(appKey)
            : AgoraChatOptions.FromAppId(appKey);
        options.EnableConsoleLog = true;
        var initError = client.InitializeSdk(options);
        if (initError is not null)
        {
            Append($"could not initialise the SDK: {initError.ErrorDescription}");
            LoginButton.IsEnabled = true;
            return;
        }

        _clientDelegate = new ClientDelegate(this);
        client.AddDelegate(_clientDelegate, queue: null);

        var login = await client.LoginAsync(username, token);
        if (login.Error is not null)
        {
            Append($"login failed: {login.Error.ErrorDescription}");
            LoginButton.IsEnabled = true;
            return;
        }

        _peer = peer;

        // ChatManager is only non-null once the client is initialised, which InitializeSdk above
        // guarantees.
        _managerDelegate = new ManagerDelegate(this);
        client.ChatManager!.AddDelegate(_managerDelegate, queue: null);

        Append($"logged in as {username}");
        SetLoggedIn(true);
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        // unbindDeviceToken only matters to an app that registered for APNs.
        await AgoraChatClient.SharedClient.LogoutAsync(unbindDeviceToken: false);

        _managerDelegate = null;
        _clientDelegate = null;
        _peer = null;
        Append("logged out");
        SetLoggedIn(false);
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        var text = MessageEntry.Text;
        if (_peer is null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // The SDK fills From from the logged-in user and To from the conversation id, which for a
        // one-to-one chat is simply the peer's username.
        var body = new AgoraChatTextMessageBody(text);
        var message = new AgoraChatMessage(_peer, body, ext: null);

        var send = await AgoraChatClient.SharedClient.ChatManager!.SendMessageAsync(message, progress: null);
        if (send.Error is not null)
        {
            Append($"send failed: {send.Error.ErrorDescription}");
            return;
        }

        // A sender does not receive its own message back through MessagesDidReceive — echo it
        // locally.
        Append($"you: {text}");
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

    /// <summary>Connection-level callbacks — what the façade turns into events.</summary>
    private sealed class ClientDelegate(MainPage owner) : AgoraChatClientDelegate
    {
        public override void ConnectionStateDidChange(AgoraChatConnectionState state) =>
            owner.Append($"connection: {state}");

        public override void UserAccountDidForcedToLogout(AgoraChatError? error) =>
            owner.Append($"logged out by the server: {error?.ErrorDescription}");
    }

    /// <summary>Message-level callbacks — the other half the façade turns into events.</summary>
    private sealed class ManagerDelegate(MainPage owner) : AgoraChatManagerDelegate
    {
        public override void MessagesDidReceive(AgoraChatMessage[] messages)
        {
            foreach (var message in messages)
            {
                var text = message.Body is AgoraChatTextMessageBody body ? body.Text : "[non-text]";
                owner.Append($"{message.From}: {text}");
            }
        }

        public override void ConversationListDidUpdate(AgoraChatConversation[] conversations)
        {
            // Not shown: this sample tracks only the one peer conversation entered above.
        }

        public override void MessageStatusDidChange(AgoraChatMessage message, AgoraChatError? error)
        {
            // Not shown: SendMessageAsync's own result above already reports success or failure.
        }
    }
}
