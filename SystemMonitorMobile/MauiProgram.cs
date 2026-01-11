using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SystemMonitorMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json")
                .GetAwaiter()
                .GetResult();
            builder.Configuration.AddJsonStream(stream);
        }
        catch (Exception)
        {
            // Optional configuration file.
        }

        var settings = new CollectorSettings();
        builder.Configuration.Bind(nameof(CollectorSettings), settings);
        builder.Services.AddSingleton(settings);
        builder.Services.AddHttpClient<CollectorApiClient>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
