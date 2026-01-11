using System.Security.Principal;
using Microsoft.Extensions.Options;

namespace SystemMonitorService;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly MetricsCollector _collector;
    private readonly SqliteStorage _storage;
    private readonly MetricsPusher _pusher;
    private readonly MonitorSettings _settings;

    public Worker(
        ILogger<Worker> logger,
        MetricsCollector collector,
        SqliteStorage storage,
        MetricsPusher pusher,
        IOptions<MonitorSettings> options)
    {
        _logger = logger;
        _collector = collector;
        _storage = storage;
        _pusher = pusher;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogElevationStatus();

        var collectTask = CollectLoopAsync(stoppingToken);
        var pushTask = PushLoopAsync(stoppingToken);
        var cleanupTask = CleanupLoopAsync(stoppingToken);

        await Task.WhenAll(collectTask, pushTask, cleanupTask);
    }

    private async Task CollectLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var metrics = _collector.Collect();
                await _storage.InsertSampleAsync(metrics, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect metrics.");
            }
        }
    }

    private async Task PushLoopAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _settings.PushIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _pusher.PushPendingAsync(stoppingToken);
        }
    }

    private async Task CleanupLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, _settings.RetentionDays));
                await _storage.CleanupOlderThanAsync(cutoff, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup old metrics.");
            }
        }
    }

    private void LogElevationStatus()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                _logger.LogWarning("Service is not running with administrative privileges.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to determine elevation status.");
        }
    }
}
