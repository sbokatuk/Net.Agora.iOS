using Microsoft.Extensions.Logging;

namespace Net.Agora.Sample.Signaling.iOS;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>();
        // No handlers to register: RTM renders nothing.

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
