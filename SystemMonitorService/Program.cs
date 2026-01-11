using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SystemMonitorService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService();

builder.Services.Configure<MonitorSettings>(
    builder.Configuration.GetSection(MonitorSettings.SectionName));

builder.Services.AddSingleton<MetricsCollector>();
builder.Services.AddSingleton<SqliteStorage>();
builder.Services.AddHttpClient<MetricsPusher>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
