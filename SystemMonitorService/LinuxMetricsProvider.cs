using System.Diagnostics;
using System.Globalization;

namespace SystemMonitorService;

public sealed class LinuxMetricsProvider : IMetricsProvider
{
    private readonly ILogger _logger;
    private (long Idle, long Total) _lastCpuTicks;
    private readonly Dictionary<int, (TimeSpan Cpu, DateTimeOffset Time)> _processState = new();

    public LinuxMetricsProvider(ILogger logger)
    {
        _logger = logger;
        _lastCpuTicks = ReadCpuTicks();
    }

    public double GetTotalCpuPercent()
    {
        var currentTicks = ReadCpuTicks();
        var idleDelta = currentTicks.Idle - _lastCpuTicks.Idle;
        var totalDelta = currentTicks.Total - _lastCpuTicks.Total;

        _lastCpuTicks = currentTicks;

        if (totalDelta <= 0) return 0;
        
        var usedPercent = 100d * (1.0 - (double)idleDelta / totalDelta);
        return Math.Clamp(usedPercent, 0, 100);
    }

    public long GetTotalRamBytes()
    {
        var memInfo = ReadMemInfo();
        return memInfo.TryGetValue("MemTotal", out var val) ? val : 0;
    }

    public long GetUsedRamBytes(long totalBytes)
    {
        var memInfo = ReadMemInfo();
        if (memInfo.TryGetValue("MemAvailable", out var available))
        {
            return totalBytes - available;
        }
        if (memInfo.TryGetValue("MemFree", out var free))
        {
            // Fallback if MemAvailable is missing (older kernels)
            return totalBytes - free;
        }
        return 0;
    }

    public IReadOnlyList<DriveSnapshot> GetDrives()
    {
        var drives = new List<DriveSnapshot>();
        try
        {
            // For simplicity on Linux, we can use DriveInfo.GetDrives() as it's cross-platform in .NET
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Network) continue;
                if (!drive.IsReady) continue;
                
                var total = drive.TotalSize;
                var used = total - drive.AvailableFreeSpace;
                drives.Add(new DriveSnapshot(drive.Name, total, used));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read drives on Linux.");
        }
        return drives;
    }

    public IReadOnlyList<ProcessSnapshot> GetProcesses(DateTimeOffset now)
    {
        var snapshots = new List<ProcessSnapshot>();
        try
        {
            // We use Process.GetProcesses() as it works on Linux too, 
            // but we need to calculate CPU diff manually just like on Windows.
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    var totalCpu = process.TotalProcessorTime;
                    var ramBytes = process.WorkingSet64;
                    var cpuPercent = 0d;

                    if (_processState.TryGetValue(process.Id, out var previous))
                    {
                        var deltaCpu = totalCpu - previous.Cpu;
                        var deltaTime = now - previous.Time;

                        if (deltaTime.TotalMilliseconds > 0)
                        {
                            cpuPercent = (deltaCpu.TotalMilliseconds / (deltaTime.TotalMilliseconds * Environment.ProcessorCount)) * 100d;
                        }
                    }

                    snapshots.Add(new ProcessSnapshot(
                        process.Id,
                        process.ProcessName,
                        Math.Clamp(cpuPercent, 0, 100),
                        ramBytes));

                    _processState[process.Id] = (totalCpu, now);
                }
                catch { /* Access denied or process exited */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read processes on Linux.");
        }
        return snapshots;
    }

    private (long Idle, long Total) ReadCpuTicks()
    {
        try
        {
            var firstLine = File.ReadLines("/proc/stat").First();
            var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) return (0, 0);

            // cpu  user nice system idle iowait ...
            long user = long.Parse(parts[1]);
            long nice = long.Parse(parts[2]);
            long system = long.Parse(parts[3]);
            long idle = long.Parse(parts[4]);
            long iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;
            long irq = parts.Length > 6 ? long.Parse(parts[6]) : 0;
            long softirq = parts.Length > 7 ? long.Parse(parts[7]) : 0;

            long totalIdle = idle + iowait;
            long totalActive = user + nice + system + irq + softirq;
            long total = totalIdle + totalActive;

            return (totalIdle, total);
        }
        catch { return (0, 0); }
    }

    private Dictionary<string, long> ReadMemInfo()
    {
        var result = new Dictionary<string, long>();
        try
        {
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var valuePart = parts[1].Trim().Split(' ')[0];
                if (long.TryParse(valuePart, out var kb))
                {
                    result[key] = kb * 1024; // Convert KB to Bytes
                }
            }
        }
        catch { }
        return result;
    }

    public void Dispose() { }
}
