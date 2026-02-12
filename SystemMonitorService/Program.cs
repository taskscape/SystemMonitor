using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SystemMonitorService;

// This line is essential for Windows Services to locate the appsettings.json file
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = Host.CreateApplicationBuilder(args);

#if OS_WINDOWS
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService();
}
#endif

builder.Services.Configure<MonitorSettings>(
    builder.Configuration.GetSection(MonitorSettings.SectionName));

builder.Services.AddSingleton<MetricsCollector>();
builder.Services.AddSingleton<SqliteStorage>();

// Configure HTTPS handling (including certificate error ignoring if set in options)
Func<IServiceProvider, HttpMessageHandler> configureHandler = sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MonitorSettings>>().Value;
    var handler = new HttpClientHandler();
    if (settings.TrustAllCertificates)
    {
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
};

builder.Services.AddHttpClient<MetricsPusher>().ConfigurePrimaryHttpMessageHandler(configureHandler);
builder.Services.AddHttpClient<CommandExecutor>().ConfigurePrimaryHttpMessageHandler(configureHandler);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
