namespace SystemCollectorService;

public sealed class StartupService : IHostedService
{
    private readonly DatabaseInitializer _initializer;

    public StartupService(DatabaseInitializer initializer)
    {
        _initializer = initializer;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _initializer.InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
