using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace SystemMonitorService;

public sealed class MetricsPusher
{
    private readonly HttpClient _httpClient;
    private readonly SqliteStorage _storage;
    private readonly MonitorSettings _settings;
    private readonly ILogger<MetricsPusher> _logger;
    private readonly string _machineName = Environment.MachineName;

    public MetricsPusher(
        HttpClient httpClient,
        SqliteStorage storage,
        IOptions<MonitorSettings> options,
        ILogger<MetricsPusher> logger)
    {
        _httpClient = httpClient;
        _storage = storage;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task PushPendingAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var pending = await _storage.GetPendingSamplesAsync(_settings.PushBatchSize, now, cancellationToken);
        if (pending.Count == 0)
        {
            return;
        }

        var payload = pending.Select(sample => new MetricsPayload(
                _machineName,
                new MachineSamplePayload(
                    sample.Machine.TimestampUtc,
                    sample.Machine.CpuPercent,
                    sample.Machine.RamUsedBytes,
                    sample.Machine.RamTotalBytes),
                sample.Drives.Select(drive => new DriveSamplePayload(
                    drive.Name,
                    drive.TotalBytes,
                    drive.UsedBytes)).ToList(),
                sample.Processes.Select(process => new ProcessSamplePayload(
                    process.ProcessId,
                    process.ProcessName,
                    process.CpuPercent,
                    process.RamBytes)).ToList()))
            .ToList();

        var ids = pending.Select(sample => sample.Machine.Id).ToList();

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                _settings.CollectorEndpoint,
                payload,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                await _storage.MarkPushedAsync(ids, cancellationToken);
                return;
            }

            _logger.LogWarning(
                "Collector responded with status {StatusCode}. Will retry later.",
                response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push metrics. Will retry later.");
        }

        var nextAttempt = now.AddSeconds(Math.Max(60, _settings.RetryDelaySeconds));
        await _storage.MarkFailedAsync(ids, nextAttempt, cancellationToken);
    }
}
