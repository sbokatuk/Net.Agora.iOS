using Microsoft.Extensions.Logging;

namespace Net.Agora.Sample.iOS;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            // Registers the handler for AgoraVideoView. MAUI does not discover handlers on its
            // own, so without this the view renders nothing. The façade repository wraps this
            // registration in a UseAgoraVideo() extension; here the raw call is the point — this
            // sample shows everything a consumer of the bare binding has to wire up themselves.
            .ConfigureMauiHandlers(handlers =>
                handlers.AddHandler<AgoraVideoView, AgoraVideoViewHandler>());

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
