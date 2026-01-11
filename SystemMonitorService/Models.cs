namespace SystemMonitorService;

public sealed record MachineSampleRecord(
    long Id,
    DateTimeOffset TimestampUtc,
    double CpuPercent,
    long RamUsedBytes,
    long RamTotalBytes);

public sealed record DriveSampleRecord(
    long Id,
    long MachineSampleId,
    string Name,
    long TotalBytes,
    long UsedBytes);

public sealed record ProcessSampleRecord(
    long Id,
    long MachineSampleId,
    int ProcessId,
    string ProcessName,
    double CpuPercent,
    long RamBytes);

public sealed record SampleEnvelope(
    MachineSampleRecord Machine,
    IReadOnlyList<DriveSampleRecord> Drives,
    IReadOnlyList<ProcessSampleRecord> Processes);

public sealed record MetricsPayload(
    string MachineName,
    MachineSamplePayload Machine,
    IReadOnlyList<DriveSamplePayload> Drives,
    IReadOnlyList<ProcessSamplePayload> Processes);

public sealed record MachineSamplePayload(
    DateTimeOffset TimestampUtc,
    double CpuPercent,
    long RamUsedBytes,
    long RamTotalBytes);

public sealed record DriveSamplePayload(
    string Name,
    long TotalBytes,
    long UsedBytes);

public sealed record ProcessSamplePayload(
    int ProcessId,
    string ProcessName,
    double CpuPercent,
    long RamBytes);
