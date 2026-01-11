namespace SystemCollectorService;

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

public sealed record MachineSummaryDto(
    string MachineName,
    DateTimeOffset LastSeenUtc);

public sealed record MachineCurrentDto(
    string MachineName,
    DateTimeOffset TimestampUtc,
    double CpuPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    IReadOnlyList<DriveSnapshotDto> Drives);

public sealed record DriveSnapshotDto(
    string Name,
    long TotalBytes,
    long UsedBytes);

public sealed record HistoryPointDto(
    DateTimeOffset TimestampUtc,
    double CpuPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    long DriveUsedBytes,
    long DriveTotalBytes);
