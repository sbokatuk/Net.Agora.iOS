using Microsoft.Maui.Handlers;
using UIKit;

namespace Net.Agora.Sample.Fastboard.iOS;

/// <summary>
/// A MAUI view that hosts Fastboard's own board view. Fastboard builds its board (with toolbar)
/// itself once <see cref="Net.Agora.Fastboard.iOS.AgoraFastboard"/> is constructed, so this handler
/// wraps a plain container and <see cref="CreateBoard"/> adds Fastboard's view as a subview of it
/// — the same technique the cross-platform façade's <c>Net.Agora.Fastboard.Maui</c> package uses
/// for iOS, reproduced here directly against the raw binding since this sample does not reference
/// that package.
/// </summary>
public class FastboardHostView : Microsoft.Maui.Controls.View
{
    /// <summary>
    /// Creates the board once this view has been realised — that is, after the page it is on has
    /// appeared. Calling it earlier throws, because the native container does not exist yet.
    /// </summary>
    /// <exception cref="InvalidOperationException">The view has no handler yet.</exception>
    public Net.Agora.Fastboard.iOS.AgoraFastboard CreateBoard(Net.Agora.Fastboard.iOS.AgoraFastboardConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (Handler?.PlatformView is not UIView container)
        {
            throw new InvalidOperationException(
                "the Fastboard host view has no native counterpart yet. Create the board after " +
                "the page has appeared rather than in its constructor.");
        }

        var board = new Net.Agora.Fastboard.iOS.AgoraFastboard(configuration);

        // Fastboard built the view; this puts it inside the one MAUI laid out. Sized to fill,
        // because MAUI owns the frame and Fastboard lays its toolbar out within whatever it gets.
        var boardView = board.View;
        boardView.Frame = container.Bounds;
        boardView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        container.AddSubview(boardView);

        return board;
    }
}

/// <summary>A plain container: Fastboard creates its own view, and <see cref="FastboardHostView.CreateBoard"/> adds it as a subview once the board exists.</summary>
public class FastboardHostViewHandler : ViewHandler<FastboardHostView, UIView>
{
    public static readonly IPropertyMapper<FastboardHostView, FastboardHostViewHandler> Mapper =
        new PropertyMapper<FastboardHostView, FastboardHostViewHandler>(ViewHandler.ViewMapper);

    public FastboardHostViewHandler() : base(Mapper)
    {
    }

    protected override UIView CreatePlatformView() => new();
}
