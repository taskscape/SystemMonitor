using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace SystemMonitorService;

public sealed class SqliteStorage
{
    private readonly ILogger<SqliteStorage> _logger;
    private readonly MonitorSettings _settings;
    private readonly string _connectionString;

    public SqliteStorage(ILogger<SqliteStorage> logger, IOptions<MonitorSettings> options)
    {
        _logger = logger;
        _settings = options.Value;

        var dbPath = string.IsNullOrWhiteSpace(_settings.DatabasePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SystemMonitorService",
                "monitor.db")
            : _settings.DatabasePath;

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    public void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS machine_samples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp_utc TEXT NOT NULL,
                cpu_percent REAL NOT NULL,
                ram_used_bytes INTEGER NOT NULL,
                ram_total_bytes INTEGER NOT NULL,
                pending_push INTEGER NOT NULL,
                next_attempt_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS drive_samples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                machine_sample_id INTEGER NOT NULL,
                name TEXT NOT NULL,
                total_bytes INTEGER NOT NULL,
                used_bytes INTEGER NOT NULL,
                FOREIGN KEY (machine_sample_id) REFERENCES machine_samples(id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS process_samples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                machine_sample_id INTEGER NOT NULL,
                process_id INTEGER NOT NULL,
                process_name TEXT NOT NULL,
                cpu_percent REAL NOT NULL,
                ram_bytes INTEGER NOT NULL,
                FOREIGN KEY (machine_sample_id) REFERENCES machine_samples(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_machine_samples_pending ON machine_samples(pending_push, next_attempt_utc);
            CREATE INDEX IF NOT EXISTS idx_machine_samples_timestamp ON machine_samples(timestamp_utc);
            """;
        command.ExecuteNonQuery();
    }

    public async Task<long> InsertSampleAsync(CollectedMetrics metrics, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var insertMachine = connection.CreateCommand();
        insertMachine.CommandText = """
            INSERT INTO machine_samples
            (timestamp_utc, cpu_percent, ram_used_bytes, ram_total_bytes, pending_push, next_attempt_utc)
            VALUES ($timestamp, $cpu, $ram_used, $ram_total, 1, $next_attempt);
            SELECT last_insert_rowid();
            """;
        insertMachine.Parameters.AddWithValue("$timestamp", metrics.TimestampUtc.UtcDateTime.ToString("O"));
        insertMachine.Parameters.AddWithValue("$cpu", metrics.CpuPercent);
        insertMachine.Parameters.AddWithValue("$ram_used", metrics.RamUsedBytes);
        insertMachine.Parameters.AddWithValue("$ram_total", metrics.RamTotalBytes);
        insertMachine.Parameters.AddWithValue("$next_attempt", metrics.TimestampUtc.UtcDateTime.ToString("O"));
        insertMachine.Transaction = transaction;

        var id = (long)(await insertMachine.ExecuteScalarAsync(cancellationToken) ?? 0);

        foreach (var drive in metrics.Drives)
        {
            var insertDrive = connection.CreateCommand();
            insertDrive.CommandText = """
                INSERT INTO drive_samples (machine_sample_id, name, total_bytes, used_bytes)
                VALUES ($machine_id, $name, $total, $used);
                """;
            insertDrive.Parameters.AddWithValue("$machine_id", id);
            insertDrive.Parameters.AddWithValue("$name", drive.Name);
            insertDrive.Parameters.AddWithValue("$total", drive.TotalBytes);
            insertDrive.Parameters.AddWithValue("$used", drive.UsedBytes);
            insertDrive.Transaction = transaction;
            await insertDrive.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var process in metrics.Processes)
        {
            var insertProcess = connection.CreateCommand();
            insertProcess.CommandText = """
                INSERT INTO process_samples (machine_sample_id, process_id, process_name, cpu_percent, ram_bytes)
                VALUES ($machine_id, $pid, $name, $cpu, $ram);
                """;
            insertProcess.Parameters.AddWithValue("$machine_id", id);
            insertProcess.Parameters.AddWithValue("$pid", process.ProcessId);
            insertProcess.Parameters.AddWithValue("$name", process.ProcessName);
            insertProcess.Parameters.AddWithValue("$cpu", process.CpuPercent);
            insertProcess.Parameters.AddWithValue("$ram", process.RamBytes);
            insertProcess.Transaction = transaction;
            await insertProcess.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    public async Task<IReadOnlyList<SampleEnvelope>> GetPendingSamplesAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var results = new List<SampleEnvelope>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var selectMachines = connection.CreateCommand();
        selectMachines.CommandText = """
            SELECT id, timestamp_utc, cpu_percent, ram_used_bytes, ram_total_bytes
            FROM machine_samples
            WHERE pending_push = 1 AND next_attempt_utc <= $now
            ORDER BY timestamp_utc
            LIMIT $limit;
            """;
        selectMachines.Parameters.AddWithValue("$now", now.UtcDateTime.ToString("O"));
        selectMachines.Parameters.AddWithValue("$limit", batchSize);

        var machineRows = new List<MachineSampleRecord>();
        await using (var reader = await selectMachines.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                machineRows.Add(new MachineSampleRecord(
                    reader.GetInt64(0),
                    DateTimeOffset.Parse(reader.GetString(1)),
                    reader.GetDouble(2),
                    reader.GetInt64(3),
                    reader.GetInt64(4)));
            }
        }

        foreach (var machine in machineRows)
        {
            var drives = await GetDrivesAsync(connection, machine.Id, cancellationToken);
            var processes = await GetProcessesAsync(connection, machine.Id, cancellationToken);
            results.Add(new SampleEnvelope(machine, drives, processes));
        }

        return results;
    }

    public async Task MarkPushedAsync(IReadOnlyCollection<long> machineSampleIds, CancellationToken cancellationToken)
    {
        if (machineSampleIds.Count == 0)
        {
            return;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var idList = string.Join(",", machineSampleIds);
        var deleteProcesses = connection.CreateCommand();
        deleteProcesses.CommandText = $"DELETE FROM process_samples WHERE machine_sample_id IN ({idList});";
        deleteProcesses.Transaction = transaction;
        await deleteProcesses.ExecuteNonQueryAsync(cancellationToken);

        var deleteDrives = connection.CreateCommand();
        deleteDrives.CommandText = $"DELETE FROM drive_samples WHERE machine_sample_id IN ({idList});";
        deleteDrives.Transaction = transaction;
        await deleteDrives.ExecuteNonQueryAsync(cancellationToken);

        var deleteMachines = connection.CreateCommand();
        deleteMachines.CommandText = $"DELETE FROM machine_samples WHERE id IN ({idList});";
        deleteMachines.Transaction = transaction;
        await deleteMachines.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        IReadOnlyCollection<long> machineSampleIds,
        DateTimeOffset nextAttemptUtc,
        CancellationToken cancellationToken)
    {
        if (machineSampleIds.Count == 0)
        {
            return;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var idList = string.Join(",", machineSampleIds);
        var update = connection.CreateCommand();
        update.CommandText = $"""
            UPDATE machine_samples
            SET next_attempt_utc = $next_attempt
            WHERE id IN ({idList});
            """;
        update.Parameters.AddWithValue("$next_attempt", nextAttemptUtc.UtcDateTime.ToString("O"));
        update.Transaction = transaction;
        await update.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CleanupOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var selectIds = connection.CreateCommand();
        selectIds.CommandText = """
            SELECT id FROM machine_samples WHERE timestamp_utc < $cutoff;
            """;
        selectIds.Parameters.AddWithValue("$cutoff", cutoffUtc.UtcDateTime.ToString("O"));
        selectIds.Transaction = transaction;

        var ids = new List<long>();
        await using (var reader = await selectIds.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetInt64(0));
            }
        }

        if (ids.Count > 0)
        {
            var idList = string.Join(",", ids);
            var deleteProcesses = connection.CreateCommand();
            deleteProcesses.CommandText = $"DELETE FROM process_samples WHERE machine_sample_id IN ({idList});";
            deleteProcesses.Transaction = transaction;
            await deleteProcesses.ExecuteNonQueryAsync(cancellationToken);

            var deleteDrives = connection.CreateCommand();
            deleteDrives.CommandText = $"DELETE FROM drive_samples WHERE machine_sample_id IN ({idList});";
            deleteDrives.Transaction = transaction;
            await deleteDrives.ExecuteNonQueryAsync(cancellationToken);

            var deleteMachines = connection.CreateCommand();
            deleteMachines.CommandText = $"DELETE FROM machine_samples WHERE id IN ({idList});";
            deleteMachines.Transaction = transaction;
            await deleteMachines.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<DriveSampleRecord>> GetDrivesAsync(
        SqliteConnection connection,
        long machineSampleId,
        CancellationToken cancellationToken)
    {
        var drives = new List<DriveSampleRecord>();
        var select = connection.CreateCommand();
        select.CommandText = """
            SELECT id, name, total_bytes, used_bytes
            FROM drive_samples
            WHERE machine_sample_id = $machine_id;
            """;
        select.Parameters.AddWithValue("$machine_id", machineSampleId);

        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            drives.Add(new DriveSampleRecord(
                reader.GetInt64(0),
                machineSampleId,
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetInt64(3)));
        }

        return drives;
    }

    private static async Task<IReadOnlyList<ProcessSampleRecord>> GetProcessesAsync(
        SqliteConnection connection,
        long machineSampleId,
        CancellationToken cancellationToken)
    {
        var processes = new List<ProcessSampleRecord>();
        var select = connection.CreateCommand();
        select.CommandText = """
            SELECT id, process_id, process_name, cpu_percent, ram_bytes
            FROM process_samples
            WHERE machine_sample_id = $machine_id;
            """;
        select.Parameters.AddWithValue("$machine_id", machineSampleId);

        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            processes.Add(new ProcessSampleRecord(
                reader.GetInt64(0),
                machineSampleId,
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetDouble(3),
                reader.GetInt64(4)));
        }

        return processes;
    }
}
