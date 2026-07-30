using Microsoft.Maui.Handlers;
using Net.Agora.Whiteboard.iOS;

namespace Net.Agora.Sample.Whiteboard.iOS;

/// <summary>
/// A MAUI view whose native counterpart <b>is</b> the board itself. Unlike Fastboard — which
/// builds its own view once the client is constructed — the plain Whiteboard SDK takes a
/// <see cref="WhiteBoardView"/> the app supplies, so the handler creates one directly and
/// <see cref="Board"/> hands it to <see cref="WhiteSDK"/>'s constructor. Same technique the
/// cross-platform façade's <c>Net.Agora.Whiteboard.Maui</c> package uses for iOS, reproduced here
/// directly against the raw binding since this sample does not reference that package.
/// </summary>
public class WhiteboardHostView : Microsoft.Maui.Controls.View
{
    /// <summary>
    /// The native board view, once this view has been realised — that is, after the page it is on
    /// has appeared. Null earlier, because the native view does not exist yet.
    /// </summary>
    public WhiteBoardView? Board => Handler?.PlatformView as WhiteBoardView;
}

public class WhiteboardHostViewHandler : ViewHandler<WhiteboardHostView, WhiteBoardView>
{
    public static readonly IPropertyMapper<WhiteboardHostView, WhiteboardHostViewHandler> Mapper =
        new PropertyMapper<WhiteboardHostView, WhiteboardHostViewHandler>(ViewHandler.ViewMapper);

    public WhiteboardHostViewHandler() : base(Mapper)
    {
    }

    protected override WhiteBoardView CreatePlatformView() => new();
}
