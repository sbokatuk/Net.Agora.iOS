using Net.Agora.Fastboard.iOS;

namespace Net.Agora.Sample.Fastboard.iOS;

/// <summary>
/// A whiteboard with Fastboard's own toolbar, driven through the <b>raw</b>
/// <c>Net.Agora.Fastboard.iOS</c> binding rather than the cross-platform façade — the point of
/// this sample is to show what <c>Net.Agora.Fastboard</c> hides: <see cref="AgoraFastboard"/>
/// builds and lays out its own <see cref="UIKit.UIView"/>, and its outcome — join, fail, get
/// kicked, change phase — arrives entirely through <see cref="AgoraFastboardDelegate"/> rather
/// than a completion block, which is why <c>Join()</c> itself returns nothing.
///
/// The three identifiers are the same three the plain Whiteboard sample uses: the App Identifier
/// from the Agora Console under Interactive Whiteboard, and the room UUID and token from your own
/// server's call to the whiteboard REST API.
/// </summary>
public partial class MainPage : ContentPage
{
    private AgoraFastboard? _board;
    private FastboardDelegate? _delegate;
    private bool _writable = true;

    public MainPage()
    {
        InitializeComponent();
    }

    private void OnJoinClicked(object sender, EventArgs e)
    {
        var appIdentifier = AppIdentifierEntry.Text?.Trim();
        var roomUuid = RoomUuidEntry.Text?.Trim();
        var roomToken = RoomTokenEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appIdentifier) || string.IsNullOrEmpty(roomUuid) || string.IsNullOrEmpty(roomToken))
        {
            Status("enter an App Identifier, a room UUID and a room token first");
            return;
        }

        JoinButton.IsEnabled = false;

        // CreateBoard() is FastboardHostView's job: it constructs AgoraFastboard, which builds its
        // own board view, and adds that view as a subview of the container MAUI laid out.
        var configuration = new AgoraFastboardConfiguration(appIdentifier, roomUuid, roomToken, userUid: "ios-user");
        _board = Board.CreateBoard(configuration);

        _delegate = new FastboardDelegate(this);
        _board.Delegate = _delegate;

        // Asynchronous underneath: Join() itself returns nothing, and the outcome arrives on
        // FastboardDelegate below — FastboardDidJoin or FastboardDidFail.
        _board.Join();
        Status("joining…");
    }

    private void OnLeaveClicked(object sender, EventArgs e)
    {
        _board?.Disconnect();
        _board = null;
        _delegate = null;

        Status("left the room");
        SetJoined(false);
    }

    private async void OnReadOnlyClicked(object sender, EventArgs e)
    {
        if (_board is null)
        {
            return;
        }

        // Fastboard hides its own drawing controls while read-only, so the toolbar follows this
        // without the app touching it.
        _writable = !_writable;
        var errorMessage = await _board.SetWritableAsync(_writable);
        if (errorMessage is not null)
        {
            Status($"failed: {errorMessage}");
            return;
        }

        ReadOnlyButton.Text = _writable ? "Read-only" : "Writable";
        Status(_writable ? "you can draw" : "you are a viewer");
    }

    // Driving the board from code while the toolbar is on screen: both change the same state, and
    // the toolbar updates to match. Colour is netless's own [r, g, b] array of 0..255 values.
    private void OnRedPencilClicked(object sender, EventArgs e)
    {
        _board?.SetAppliance("pencil", color: [232, 17, 35], strokeWidth: 4);
        Status("tool: red pencil");
    }

    private void SetJoined(bool joined)
    {
        JoinButton.IsEnabled = !joined;
        LeaveButton.IsEnabled = joined;
        ReadOnlyButton.IsEnabled = joined;
        RedPencilButton.IsEnabled = joined;
    }

    private void Status(string message) =>
        MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {message}");

    /// <summary>Every member here is required: Fastboard's outcome has nowhere else to go.</summary>
    private sealed class FastboardDelegate(MainPage owner) : AgoraFastboardDelegate
    {
        public override void FastboardDidJoin()
        {
            owner._writable = true;
            owner.Status("joined — the toolbar is live");
            MainThread.BeginInvokeOnMainThread(() => owner.SetJoined(true));
        }

        public override void FastboardDidFail(string message)
        {
            owner.Status($"failed: {message}");
            MainThread.BeginInvokeOnMainThread(() => owner.JoinButton.IsEnabled = true);
        }

        public override void FastboardWasKicked(string reason)
        {
            owner.Status($"removed from the room: {reason}");
            MainThread.BeginInvokeOnMainThread(() => owner.SetJoined(false));
        }

        public override void FastboardPhaseDidChange(AgoraFastboardPhase phase) =>
            owner.Status($"room: {phase}");
    }
}
