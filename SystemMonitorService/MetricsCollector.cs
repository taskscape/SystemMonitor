using System.Diagnostics;
using System.Management;

namespace SystemMonitorService;

public sealed class MetricsCollector : IDisposable
{
    private readonly ILogger<MetricsCollector> _logger;
    private readonly PerformanceCounter? _totalCpuCounter;
    private readonly PerformanceCounter? _availableMemoryCounter;
    private readonly Lazy<long> _totalRamBytes;
    private readonly Dictionary<int, ProcessSampleState> _processState = new();

    public MetricsCollector(ILogger<MetricsCollector> logger)
    {
        _logger = logger;
        _totalRamBytes = new Lazy<long>(ReadTotalPhysicalMemoryBytes);

        try
        {
            _totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _totalCpuCounter.NextValue();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CPU performance counter unavailable. Total CPU will report 0.");
        }

        try
        {
            _availableMemoryCounter = new PerformanceCounter("Memory", "Available Bytes");
            _availableMemoryCounter.NextValue();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memory performance counter unavailable. RAM used will report 0.");
        }
    }

    public CollectedMetrics Collect()
    {
        var now = DateTimeOffset.UtcNow;
        var cpuPercent = ReadTotalCpuPercent();
        var ramTotalBytes = _totalRamBytes.Value;
        var ramUsedBytes = ReadUsedRamBytes(ramTotalBytes);
        var drives = ReadDrives();
        var processes = ReadProcesses(now);

        return new CollectedMetrics(
            now,
            cpuPercent,
            ramUsedBytes,
            ramTotalBytes,
            drives,
            processes);
    }

    private double ReadTotalCpuPercent()
    {
        if (_totalCpuCounter is null)
        {
            return 0;
        }

        try
        {
            var value = _totalCpuCounter.NextValue();
            return Math.Clamp(value, 0, 100);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read total CPU percent.");
            return 0;
        }
    }

    private long ReadUsedRamBytes(long totalBytes)
    {
        if (_availableMemoryCounter is null)
        {
            return 0;
        }

        try
        {
            var available = (long)_availableMemoryCounter.NextValue();
            if (totalBytes == 0)
            {
                return 0;
            }

            var used = totalBytes - available;
            return used < 0 ? 0 : used;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read available RAM.");
            return 0;
        }
    }

    private long ReadTotalPhysicalMemoryBytes()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                if (obj["TotalPhysicalMemory"] is ulong total)
                {
                    return (long)total;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read total physical memory.");
        }

        return 0;
    }

    private static IReadOnlyList<DriveSnapshot> ReadDrives()
    {
        var drives = new List<DriveSnapshot>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
            {
                continue;
            }

            var total = drive.TotalSize;
            var used = total - drive.AvailableFreeSpace;
            drives.Add(new DriveSnapshot(drive.Name, total, used));
        }

        return drives;
    }

    private IReadOnlyList<ProcessSnapshot> ReadProcesses(DateTimeOffset now)
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
            catch (Exception)
            {
                // Processes can exit or deny access between enumeration and read.
            }
        }

        _processState.Clear();
        foreach (var pair in nextState)
        {
            _processState[pair.Key] = pair.Value;
        }

        return snapshots;
    }

    public void Dispose()
    {
        _totalCpuCounter?.Dispose();
        _availableMemoryCounter?.Dispose();
    }

    private sealed record ProcessSampleState(TimeSpan TotalProcessorTime, DateTimeOffset TimestampUtc);
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
