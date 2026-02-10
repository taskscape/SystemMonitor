namespace SystemCollectorService;

public sealed class CollectorSettings
{
    public const string SectionName = "CollectorSettings";

    public string ConnectionString { get; set; } = "Data Source=system_monitor.db";

    public string ListenUrl { get; set; } = "https://0.0.0.0:5101";

    public int RetentionDays { get; set; } = 7;
}
