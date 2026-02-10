using Microsoft.Extensions.Options;

namespace SystemCollectorService;

public sealed class DatabaseCleanupService : BackgroundService
{
    private readonly ILogger<DatabaseCleanupService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<CollectorSettings> _settings;

    public DatabaseCleanupService(
        ILogger<DatabaseCleanupService> logger,
        IServiceProvider serviceProvider,
        IOptions<CollectorSettings> settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Database Cleanup Service started. Retention: {Days} days.", _settings.Value.RetentionDays);

        // Run every hour
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<CollectorRepository>();

                await repository.CleanupOldDataAsync(_settings.Value.RetentionDays, stoppingToken);
                
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database cleanup.");
                // Wait a bit before retrying if it failed
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
