using Microsoft.Maui.Handlers;
using UIKit;

namespace Net.Agora.Sample.iOS;

/// <summary>
/// A MAUI view that Agora renders video into. <c>SetupLocalVideo</c>/<c>SetupRemoteVideo</c> take
/// a plain <c>UIView</c> and the SDK adds its own renderer as a subview, so there is nothing
/// SDK-specific to create here — the handler below exists only to give the MAUI layout a native
/// view whose handle can be passed to <see cref="Video.iOS.AgoraRtcVideoCanvas"/>.
///
/// A custom view rather than a wrapped <c>ContentView</c> because the canvas wants the native
/// view itself, not a MAUI wrapper around it. This is the same shape as the façade repository's
/// Net.Agora.Video.Maui.AgoraVideoView, minus the Android half — reach for that package instead
/// of copying this file if you are not consuming the raw binding on purpose.
/// </summary>
public class AgoraVideoView : View
{
    /// <summary>The native view Agora renders into, once the handler has been created.</summary>
    public UIView? PlatformView => Handler?.PlatformView as UIView;
}

/// <summary>Maps <see cref="AgoraVideoView"/> to a bare black <c>UIView</c>.</summary>
public partial class AgoraVideoViewHandler : ViewHandler<AgoraVideoView, UIView>
{
    /// <summary>The view has no properties of its own; the mapper exists because a handler needs one.</summary>
    public static readonly IPropertyMapper<AgoraVideoView, AgoraVideoViewHandler> VideoMapper =
        new PropertyMapper<AgoraVideoView, AgoraVideoViewHandler>(ViewMapper);

    /// <summary>Creates the handler. Registered in <see cref="MauiProgram"/>.</summary>
    public AgoraVideoViewHandler() : base(VideoMapper)
    {
    }

    /// <inheritdoc />
    protected override UIView CreatePlatformView() => new() { BackgroundColor = UIColor.Black };
}
