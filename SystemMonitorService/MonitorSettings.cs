namespace SystemMonitorService;

public sealed class MonitorSettings
{
    public const string SectionName = "MonitorSettings";

    public string CollectorEndpoint { get; set; } = "https://collector.local/api/v1/metrics";
    public string DatabasePath { get; set; } = string.Empty;
    public int PushBatchSize { get; set; } = 50;
    public int PushIntervalSeconds { get; set; } = 10;
    public int RetryDelaySeconds { get; set; } = 60;
    public int RetentionDays { get; set; } = 7;
}
