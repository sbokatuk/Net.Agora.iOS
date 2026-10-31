using Foundation;
using Net.Agora.Whiteboard.iOS;

namespace Net.Agora.Sample.Whiteboard.iOS;

/// <summary>
/// A shared drawing surface over the Agora Interactive Whiteboard, driven through the <b>raw</b>
/// <c>Net.Agora.Whiteboard.iOS</c> binding rather than the cross-platform façade — the point of
/// this sample is to show what <c>Net.Agora.Whiteboard</c> hides: the app owns the
/// <see cref="Net.Agora.Whiteboard.iOS.WhiteBoardView"/> and constructs <see cref="WhiteSDK"/>
/// against it directly, and the room's lifecycle arrives on
/// <see cref="WhiteRoomCallbackDelegate"/> rather than .NET events.
///
/// The three identifiers come from two different places. The App Identifier is in the Agora
/// Console under Interactive Whiteboard; the room UUID and its token come from your own server's
/// call to the whiteboard REST API, because minting them needs a secret an app must not carry.
/// </summary>
public partial class MainPage : ContentPage
{
    private WhiteRoom? _room;
    private RoomCallbacks? _callbacks;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnJoinClicked(object sender, EventArgs e)
    {
        var appIdentifier = AppIdentifierEntry.Text?.Trim();
        var roomUuid = RoomUuidEntry.Text?.Trim();
        var roomToken = RoomTokenEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appIdentifier) || string.IsNullOrEmpty(roomUuid) || string.IsNullOrEmpty(roomToken))
        {
            Status("enter an App Identifier, a room UUID and a room token first");
            return;
        }

        if (Board.Board is not { } boardView)
        {
            Status("the board view has no native counterpart yet");
            return;
        }

        JoinButton.IsEnabled = false;

        try
        {
            var sdkConfig = new WhiteSdkConfiguration(appIdentifier);
            var roomConfig = new WhiteRoomConfig(roomUuid, roomToken, uid: "ios-user");

            // A room can only be joined from the region it was created in. Left empty, the SDK uses
            // its default.
            if (RegionEntry.Text?.Trim() is { Length: > 0 } region)
            {
                sdkConfig.Region = region;
                roomConfig.Region = region;
            }

            var sdk = new WhiteSDK(boardView, sdkConfig, callback: null);
            _callbacks = new RoomCallbacks(this);

            // JoinRoomAsync faults with an NSErrorException on a genuine join failure — unlike
            // WhiteRoom's own pair-shaped completions below, a join's two success values (Success,
            // Room) leave the generator's ordinary NSError convention in force.
            var result = await sdk.JoinRoomAsync(roomConfig, _callbacks);

            _room = result.Room;

            // A room starts with serialization disabled, and in that state it keeps no history:
            // FireCanUndoStepsUpdate never fires, however many strokes are drawn, and Undo and Redo
            // do nothing. Enabling it needs every client in the room on SDK 2.9.3 or later (Web
            // 2.9.2): upstream warns that app clients crash otherwise.
            _room.DisableSerialization(false);

            Status("joined — draw on the board");
            SetJoined(true);
        }
        catch (NSErrorException exception)
        {
            Status($"failed: {exception.Message}");
            _room = null;
            _callbacks = null;
            JoinButton.IsEnabled = true;
        }
    }

    private async void OnLeaveClicked(object sender, EventArgs e)
    {
        if (_room is not null)
        {
            await _room.DisconnectAsync();
        }

        _room = null;
        _callbacks = null;
        Status("left the room");
        SetJoined(false);
    }

    private void OnPencilClicked(object sender, EventArgs e) => Tool("pencil", [232, 17, 35], "pencil");

    private void OnEraserClicked(object sender, EventArgs e) => Tool("eraser", color: null, "eraser");

    private void OnUndoClicked(object sender, EventArgs e) => _room?.Undo();

    private void OnRedoClicked(object sender, EventArgs e) => _room?.Redo();

    private void OnClearClicked(object sender, EventArgs e)
    {
        // retainPpt keeps a converted document's own content on the scene and clears only what was
        // drawn over it — there is no document here, so the flag makes no difference.
        _room?.CleanScene(retainPpt: true);
        Status("cleared the page for everyone");
    }

    private void Tool(string appliance, NSNumber[]? color, string name)
    {
        if (_room is null)
        {
            return;
        }

        var state = new WhiteMemberState
        {
            CurrentApplianceName = appliance,
            StrokeColor = color,
            StrokeWidth = 4,
        };
        _room.SetMemberState(state);
        Status($"tool: {name}");
    }

    private void SetJoined(bool joined)
    {
        JoinButton.IsEnabled = !joined;
        LeaveButton.IsEnabled = joined;
        PencilButton.IsEnabled = joined;
        EraserButton.IsEnabled = joined;
        ClearButton.IsEnabled = joined;

        if (!joined)
        {
            UndoButton.IsEnabled = false;
            RedoButton.IsEnabled = false;
        }
    }

    private void Status(string message) =>
        MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {message}");

    /// <summary>Every member here is optional; only the ones this sample acts on are overridden.</summary>
    private sealed class RoomCallbacks(MainPage owner) : WhiteRoomCallbackDelegate
    {
        public override void FirePhaseChanged(WhiteRoomPhase phase) => owner.Status($"room: {phase}");

        public override void FireDisconnectWithError(string error)
        {
            owner.Status($"disconnected: {error}");
            MainThread.BeginInvokeOnMainThread(() => owner.SetJoined(false));
        }

        public override void FireKickedWithReason(string reason)
        {
            owner.Status($"removed from the room: {reason}");
            MainThread.BeginInvokeOnMainThread(() => owner.SetJoined(false));
        }

        public override void FireCanUndoStepsUpdate(nint canUndoSteps) =>
            MainThread.BeginInvokeOnMainThread(() => owner.UndoButton.IsEnabled = canUndoSteps > 0);

        public override void FireCanRedoStepsUpdate(nint canRedoSteps) =>
            MainThread.BeginInvokeOnMainThread(() => owner.RedoButton.IsEnabled = canRedoSteps > 0);
    }
}
