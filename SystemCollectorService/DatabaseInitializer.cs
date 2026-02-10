using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace SystemCollectorService;

public sealed class DatabaseInitializer
{
    private readonly CollectorSettings _settings;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IOptions<CollectorSettings> options, ILogger<DatabaseInitializer> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = _settings.ConnectionString;
            
            // Ensure the directory exists if the connection string specifies a file path
            var builder = new SqliteConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(builder.DataSource) && builder.DataSource != ":memory:")
            {
                var dir = Path.GetDirectoryName(builder.DataSource);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            await EnsureSchemaAsync(connectionString, cancellationToken);
            _logger.LogInformation("Database initialized successfully at {DataSource}.", builder.DataSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database.");
            throw;
        }
    }

    private static async Task EnsureSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Enable Write-Ahead Logging for better concurrency
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode = WAL;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS machines (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                first_seen_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS machine_samples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                machine_id INTEGER NOT NULL REFERENCES machines(id) ON DELETE CASCADE,
                timestamp_utc TEXT NOT NULL,
                cpu_percent REAL NOT NULL,
                ram_used_bytes INTEGER NOT NULL,
                ram_total_bytes INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS drive_samples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                machine_sample_id INTEGER NOT NULL REFERENCES machine_samples(id) ON DELETE CASCADE,
                name TEXT NOT NULL,
                total_bytes INTEGER NOT NULL,
                used_bytes INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS process_samples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                machine_sample_id INTEGER NOT NULL REFERENCES machine_samples(id) ON DELETE CASCADE,
                process_id INTEGER NOT NULL,
                process_name TEXT NOT NULL,
                cpu_percent REAL NOT NULL,
                ram_bytes INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS machine_minute_cache (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                machine_id INTEGER NOT NULL REFERENCES machines(id) ON DELETE CASCADE,
                bucket_start_utc TEXT NOT NULL,
                sample_count INTEGER NOT NULL,
                cpu_percent_avg REAL NOT NULL,
                ram_used_bytes_avg REAL NOT NULL,
                ram_total_bytes_avg REAL NOT NULL,
                drive_used_bytes_avg REAL NOT NULL,
                drive_total_bytes_avg REAL NOT NULL,
                UNIQUE (machine_id, bucket_start_utc)
            );

            CREATE INDEX IF NOT EXISTS idx_machine_samples_machine_time ON machine_samples(machine_id, timestamp_utc DESC);
            CREATE INDEX IF NOT EXISTS idx_machine_samples_time ON machine_samples(timestamp_utc);
            CREATE INDEX IF NOT EXISTS idx_drive_samples_machine_sample ON drive_samples(machine_sample_id);
            CREATE INDEX IF NOT EXISTS idx_process_samples_machine_sample ON process_samples(machine_sample_id);
            CREATE INDEX IF NOT EXISTS idx_machine_minute_cache_machine_time ON machine_minute_cache(machine_id, bucket_start_utc DESC);

            CREATE TABLE IF NOT EXISTS machine_commands (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                machine_id INTEGER NOT NULL REFERENCES machines(id) ON DELETE CASCADE,
                command_type TEXT NOT NULL,
                status TEXT NOT NULL DEFAULT 'pending', -- pending, executing, completed, failed
                result TEXT,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_machine_commands_machine_status ON machine_commands(machine_id, status);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
