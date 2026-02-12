namespace SystemMonitorService;

public interface IMetricsProvider : IDisposable
{
    double GetTotalCpuPercent();
    long GetUsedRamBytes(long totalBytes);
    long GetTotalRamBytes();
    IReadOnlyList<DriveSnapshot> GetDrives();
    IReadOnlyList<ProcessSnapshot> GetProcesses(DateTimeOffset now);
}
