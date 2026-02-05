namespace SystemCollectorService;

public sealed class CollectorSettings
{
    public const string SectionName = "CollectorSettings";

    public string ConnectionString { get; set; } =
        "Host=localhost;Port=5432;Database=system_monitor;Username=postgres;Password=postgres";

    public string ListenUrl { get; set; } = "https://0.0.0.0:5101";
    
    public string RabbitMqHostName { get; set; } = "localhost";
}
