using Microsoft.Extensions.Options;
using Npgsql;

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
        var builder = new NpgsqlConnectionStringBuilder(_settings.ConnectionString);
        var targetDatabase = builder.Database;
        if (string.IsNullOrWhiteSpace(targetDatabase))
        {
            targetDatabase = "system_monitor";
            builder.Database = targetDatabase;
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
        {
            Database = "postgres"
        };

        await EnsureDatabaseExistsAsync(adminBuilder.ConnectionString, targetDatabase, cancellationToken);
        await EnsureSchemaAsync(builder.ConnectionString, cancellationToken);
    }

    private async Task EnsureDatabaseExistsAsync(
        string adminConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name;";
        existsCommand.Parameters.AddWithValue("@name", databaseName);

        var exists = await existsCommand.ExecuteScalarAsync(cancellationToken) is not null;
        if (exists)
        {
            return;
        }

        _logger.LogInformation("Creating database {Database}.", databaseName);
        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"CREATE DATABASE \"{databaseName}\";";
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS machines (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                first_seen_utc TIMESTAMPTZ NOT NULL,
                last_seen_utc TIMESTAMPTZ NOT NULL
            );

            CREATE TABLE IF NOT EXISTS machine_samples (
                id BIGSERIAL PRIMARY KEY,
                machine_id INTEGER NOT NULL REFERENCES machines(id) ON DELETE CASCADE,
                timestamp_utc TIMESTAMPTZ NOT NULL,
                cpu_percent DOUBLE PRECISION NOT NULL,
                ram_used_bytes BIGINT NOT NULL,
                ram_total_bytes BIGINT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS drive_samples (
                id BIGSERIAL PRIMARY KEY,
                machine_sample_id BIGINT NOT NULL REFERENCES machine_samples(id) ON DELETE CASCADE,
                name TEXT NOT NULL,
                total_bytes BIGINT NOT NULL,
                used_bytes BIGINT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS process_samples (
                id BIGSERIAL PRIMARY KEY,
                machine_sample_id BIGINT NOT NULL REFERENCES machine_samples(id) ON DELETE CASCADE,
                process_id INTEGER NOT NULL,
                process_name TEXT NOT NULL,
                cpu_percent DOUBLE PRECISION NOT NULL,
                ram_bytes BIGINT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS machine_minute_cache (
                id BIGSERIAL PRIMARY KEY,
                machine_id INTEGER NOT NULL REFERENCES machines(id) ON DELETE CASCADE,
                bucket_start_utc TIMESTAMPTZ NOT NULL,
                sample_count INTEGER NOT NULL,
                cpu_percent_avg DOUBLE PRECISION NOT NULL,
                ram_used_bytes_avg DOUBLE PRECISION NOT NULL,
                ram_total_bytes_avg DOUBLE PRECISION NOT NULL,
                drive_used_bytes_avg DOUBLE PRECISION NOT NULL,
                drive_total_bytes_avg DOUBLE PRECISION NOT NULL,
                UNIQUE (machine_id, bucket_start_utc)
            );

            CREATE INDEX IF NOT EXISTS idx_machine_samples_machine_time ON machine_samples(machine_id, timestamp_utc DESC);
            CREATE INDEX IF NOT EXISTS idx_machine_samples_time ON machine_samples(timestamp_utc);
            CREATE INDEX IF NOT EXISTS idx_drive_samples_machine_sample ON drive_samples(machine_sample_id);
            CREATE INDEX IF NOT EXISTS idx_process_samples_machine_sample ON process_samples(machine_sample_id);
            CREATE INDEX IF NOT EXISTS idx_machine_minute_cache_machine_time ON machine_minute_cache(machine_id, bucket_start_utc DESC);

            CREATE TABLE IF NOT EXISTS machine_commands (
                id BIGSERIAL PRIMARY KEY,
                machine_id INTEGER NOT NULL REFERENCES machines(id) ON DELETE CASCADE,
                command_type TEXT NOT NULL,
                status TEXT NOT NULL DEFAULT 'pending', -- pending, executing, completed, failed
                result TEXT,
                created_at_utc TIMESTAMPTZ NOT NULL,
                updated_at_utc TIMESTAMPTZ NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_machine_commands_machine_status ON machine_commands(machine_id, status);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
