using Microsoft.Extensions.Logging;

namespace Net.Agora.Sample.Fastboard.iOS;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureMauiHandlers(handlers =>
                handlers.AddHandler<FastboardHostView, FastboardHostViewHandler>());

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
