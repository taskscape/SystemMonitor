using System.Runtime.InteropServices;

namespace SystemMonitorService;

public sealed class MetricsCollector : IDisposable
{
    private readonly ILogger<MetricsCollector> _logger;
    private readonly IMetricsProvider _provider;
    private readonly Lazy<long> _totalRamBytes;

    public MetricsCollector(ILogger<MetricsCollector> logger)
    {
        _logger = logger;

#if OS_WINDOWS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _provider = new WindowsMetricsProvider(logger);
        }
        else
#endif
        {
            _provider = new LinuxMetricsProvider(logger);
        }

        _totalRamBytes = new Lazy<long>(() => _provider.GetTotalRamBytes());
    }

    public CollectedMetrics Collect()
    {
        var now = DateTimeOffset.UtcNow;
        var cpuPercent = _provider.GetTotalCpuPercent();
        var ramTotalBytes = _totalRamBytes.Value;
        var ramUsedBytes = _provider.GetUsedRamBytes(ramTotalBytes);
        var drives = _provider.GetDrives();
        var processes = _provider.GetProcesses(now);

        return new CollectedMetrics(
            now,
            cpuPercent,
            ramUsedBytes,
            ramTotalBytes,
            drives,
            processes);
    }

    public void Dispose()
    {
        _provider.Dispose();
    }
}

public sealed record CollectedMetrics(
    DateTimeOffset TimestampUtc,
    double CpuPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    IReadOnlyList<DriveSnapshot> Drives,
    IReadOnlyList<ProcessSnapshot> Processes);

public sealed record DriveSnapshot(string Name, long TotalBytes, long UsedBytes);

public sealed record ProcessSnapshot(int ProcessId, string ProcessName, double CpuPercent, long RamBytes);
