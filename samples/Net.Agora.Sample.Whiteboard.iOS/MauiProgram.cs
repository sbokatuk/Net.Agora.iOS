using Microsoft.Extensions.Logging;

namespace Net.Agora.Sample.Whiteboard.iOS;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureMauiHandlers(handlers =>
                handlers.AddHandler<WhiteboardHostView, WhiteboardHostViewHandler>());

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
