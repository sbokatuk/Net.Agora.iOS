using Microsoft.Extensions.Logging;

namespace Net.Agora.Sample.Chat.iOS;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>();
        // No handlers to register: Chat renders nothing of its own.

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
