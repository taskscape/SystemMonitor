using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SystemMonitorService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService();

builder.Services.Configure<MonitorSettings>(
    builder.Configuration.GetSection(MonitorSettings.SectionName));

builder.Services.AddSingleton<MetricsCollector>();

builder.Services.AddSingleton<SqliteStorage>();



// Wspólna konfiguracja Handlera HTTP (SSL Bypass)

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
