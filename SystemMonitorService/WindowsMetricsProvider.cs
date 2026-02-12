#if OS_WINDOWS
using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;

namespace SystemMonitorService;

[SupportedOSPlatform("windows")]
public sealed class WindowsMetricsProvider : IMetricsProvider
{
    // ... reszta kodu klasy ...
    private readonly ILogger _logger;
    private readonly PerformanceCounter? _totalCpuCounter;
    private readonly PerformanceCounter? _availableMemoryCounter;
    private readonly Dictionary<int, ProcessSampleState> _processState = new();

    public WindowsMetricsProvider(ILogger logger)
    {
        _logger = logger;

        try
        {
            _totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _totalCpuCounter.NextValue();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CPU performance counter unavailable.");
        }

        try
        {
            _availableMemoryCounter = new PerformanceCounter("Memory", "Available Bytes");
            _availableMemoryCounter.NextValue();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memory performance counter unavailable.");
        }
    }

    public double GetTotalCpuPercent()
    {
        if (_totalCpuCounter is null) return 0;
        try
        {
            return Math.Clamp(_totalCpuCounter.NextValue(), 0, 100);
        }
        catch { return 0; }
    }

    public long GetUsedRamBytes(long totalBytes)
    {
        if (_availableMemoryCounter is null || totalBytes == 0) return 0;
        try
        {
            var available = (long)_availableMemoryCounter.NextValue();
            var used = totalBytes - available;
            return used < 0 ? 0 : used;
        }
        catch { return 0; }
    }

    public long GetTotalRamBytes()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                if (obj["TotalPhysicalMemory"] is ulong total) return (long)total;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read total physical memory.");
        }
        return 0;
    }

    public IReadOnlyList<DriveSnapshot> GetDrives()
    {
        var drives = new List<DriveSnapshot>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
            var total = drive.TotalSize;
            var used = total - drive.AvailableFreeSpace;
            drives.Add(new DriveSnapshot(drive.Name, total, used));
        }
        return drives;
    }

    public IReadOnlyList<ProcessSnapshot> GetProcesses(DateTimeOffset now)
    {
        var snapshots = new List<ProcessSnapshot>();
        var nextState = new Dictionary<int, ProcessSampleState>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var totalCpu = process.TotalProcessorTime;
                var ramBytes = process.WorkingSet64;
                var cpuPercent = 0d;

                if (_processState.TryGetValue(process.Id, out var previous))
                {
                    var deltaCpu = totalCpu - previous.TotalProcessorTime;
                    var deltaTime = now - previous.TimestampUtc;

                    if (deltaTime.TotalMilliseconds > 0)
                    {
                        cpuPercent = deltaCpu.TotalMilliseconds
                            / (deltaTime.TotalMilliseconds * Environment.ProcessorCount) * 100d;
                    }
                }

                snapshots.Add(new ProcessSnapshot(
                    process.Id,
                    process.ProcessName,
                    Math.Clamp(cpuPercent, 0, 100),
                    ramBytes));

                nextState[process.Id] = new ProcessSampleState(totalCpu, now);
            }
            catch { }
        }

        _processState.Clear();
        foreach (var pair in nextState) _processState[pair.Key] = pair.Value;

        return snapshots;
    }

    public void Dispose()
    {
        _totalCpuCounter?.Dispose();
        _availableMemoryCounter?.Dispose();
    }

    private sealed record ProcessSampleState(TimeSpan TotalProcessorTime, DateTimeOffset TimestampUtc);
}
#endif

