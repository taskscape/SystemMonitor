using Npgsql;

namespace SystemCollectorService;

public sealed class CollectorRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<CollectorRepository> _logger;

    public CollectorRepository(NpgsqlDataSource dataSource, ILogger<CollectorRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task StoreBatchAsync(IReadOnlyList<MetricsPayload> payload, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var sample in payload)
        {
            var machineId = await UpsertMachineAsync(connection, transaction, sample.MachineName, sample.Machine.TimestampUtc, cancellationToken);
            var machineSampleId = await InsertMachineSampleAsync(connection, transaction, machineId, sample.Machine, cancellationToken);
            var driveTotals = SumDrives(sample.Drives);

            if (sample.Drives.Count > 0)
            {
                await InsertDrivesAsync(connection, transaction, machineSampleId, sample.Drives, cancellationToken);
            }

            if (sample.Processes.Count > 0)
            {
                await InsertProcessesAsync(connection, transaction, machineSampleId, sample.Processes, cancellationToken);
            }

            var bucketStart = GetMinuteBucket(sample.Machine.TimestampUtc);
            await UpsertMinuteCacheAsync(
                connection,
                transaction,
                machineId,
                bucketStart,
                sample.Machine,
                driveTotals.used,
                driveTotals.total,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MachineSummaryDto>> GetMachinesAsync(CancellationToken cancellationToken)
    {
        var results = new List<MachineSummaryDto>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name, last_seen_utc
            FROM machines
            ORDER BY name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new MachineSummaryDto(
                reader.GetString(0),
                reader.GetFieldValue<DateTimeOffset>(1)));
        }

        return results;
    }

    public async Task<MachineCurrentDto?> GetCurrentAsync(string machineName, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m.name, ms.timestamp_utc, ms.cpu_percent, ms.ram_used_bytes, ms.ram_total_bytes
            FROM machines m
            JOIN machine_samples ms ON ms.machine_id = m.id
            WHERE m.name = @name
            ORDER BY ms.timestamp_utc DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@name", machineName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var dto = new MachineCurrentDto(
            reader.GetString(0),
            reader.GetFieldValue<DateTimeOffset>(1),
            reader.GetDouble(2),
            reader.GetDouble(3),
            reader.GetDouble(4),
            Array.Empty<DriveSnapshotDto>());

        await reader.CloseAsync();

        var sampleId = await GetLatestSampleIdAsync(connection, machineName, cancellationToken);
        var drives = sampleId is null
            ? Array.Empty<DriveSnapshotDto>()
            : await GetDrivesAsync(connection, sampleId.Value, cancellationToken);
        return dto with { Drives = drives };
    }

    public async Task<IReadOnlyList<HistoryPointDto>> GetHistoryAsync(
        string machineName,
        int days,
        CancellationToken cancellationToken)
    {
        var results = new List<HistoryPointDto>();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.bucket_start_utc,
                   c.cpu_percent_avg,
                   c.ram_used_bytes_avg,
                   c.ram_total_bytes_avg,
                   c.drive_used_bytes_avg,
                   c.drive_total_bytes_avg
            FROM machines m
            JOIN machine_minute_cache c ON c.machine_id = m.id
            WHERE m.name = @name AND c.bucket_start_utc >= @cutoff
            ORDER BY c.bucket_start_utc;
            """;
        command.Parameters.AddWithValue("@name", machineName);
        command.Parameters.AddWithValue("@cutoff", cutoff);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new HistoryPointDto(
                reader.GetFieldValue<DateTimeOffset>(0),
                reader.GetDouble(1),
                (long)reader.GetDouble(2),
                (long)reader.GetDouble(3),
                (long)reader.GetDouble(4),
                (long)reader.GetDouble(5)));
        }

        return results;
    }

    private static (double used, double total) SumDrives(IReadOnlyList<DriveSamplePayload> drives)
    {
        double used = 0;
        double total = 0;
        foreach (var drive in drives)
        {
            used += drive.UsedBytes;
            total += drive.TotalBytes;
        }

        return (used, total);
    }

    private static DateTimeOffset GetMinuteBucket(DateTimeOffset timestampUtc)
    {
        var utc = timestampUtc.UtcDateTime;
        var bucketUtc = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc);
        return new DateTimeOffset(bucketUtc);
    }

    private static async Task<int> UpsertMachineAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string machineName,
        DateTimeOffset lastSeenUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO machines (name, first_seen_utc, last_seen_utc)
            VALUES (@name, @seen, @seen)
            ON CONFLICT (name) DO UPDATE SET last_seen_utc = EXCLUDED.last_seen_utc
            RETURNING id;
            """;
        command.Parameters.AddWithValue("@name", machineName);
        command.Parameters.AddWithValue("@seen", lastSeenUtc);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private static async Task<long> InsertMachineSampleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int machineId,
        MachineSamplePayload machine,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO machine_samples
                (machine_id, timestamp_utc, cpu_percent, ram_used_bytes, ram_total_bytes)
            VALUES
                (@machine_id, @timestamp, @cpu, @ram_used, @ram_total)
            RETURNING id;
            """;
        command.Parameters.AddWithValue("@machine_id", machineId);
        command.Parameters.AddWithValue("@timestamp", machine.TimestampUtc);
        command.Parameters.AddWithValue("@cpu", machine.CpuPercent);
        command.Parameters.AddWithValue("@ram_used", machine.RamUsedBytes);
        command.Parameters.AddWithValue("@ram_total", machine.RamTotalBytes);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private static async Task UpsertMinuteCacheAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int machineId,
        DateTimeOffset bucketStartUtc,
        MachineSamplePayload machine,
        double driveUsedBytes,
        double driveTotalBytes,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO machine_minute_cache (
                machine_id,
                bucket_start_utc,
                sample_count,
                cpu_percent_avg,
                ram_used_bytes_avg,
                ram_total_bytes_avg,
                drive_used_bytes_avg,
                drive_total_bytes_avg
            )
            VALUES (
                @machine_id,
                @bucket_start,
                1,
                @cpu,
                @ram_used,
                @ram_total,
                @drive_used,
                @drive_total
            )
            ON CONFLICT (machine_id, bucket_start_utc) DO UPDATE SET
                sample_count = machine_minute_cache.sample_count + 1,
                cpu_percent_avg = ((machine_minute_cache.cpu_percent_avg * machine_minute_cache.sample_count) + EXCLUDED.cpu_percent_avg)
                    / (machine_minute_cache.sample_count + 1),
                ram_used_bytes_avg = ((machine_minute_cache.ram_used_bytes_avg * machine_minute_cache.sample_count) + EXCLUDED.ram_used_bytes_avg)
                    / (machine_minute_cache.sample_count + 1),
                ram_total_bytes_avg = ((machine_minute_cache.ram_total_bytes_avg * machine_minute_cache.sample_count) + EXCLUDED.ram_total_bytes_avg)
                    / (machine_minute_cache.sample_count + 1),
                drive_used_bytes_avg = ((machine_minute_cache.drive_used_bytes_avg * machine_minute_cache.sample_count) + EXCLUDED.drive_used_bytes_avg)
                    / (machine_minute_cache.sample_count + 1),
                drive_total_bytes_avg = ((machine_minute_cache.drive_total_bytes_avg * machine_minute_cache.sample_count) + EXCLUDED.drive_total_bytes_avg)
                    / (machine_minute_cache.sample_count + 1);
            """;
        command.Parameters.AddWithValue("@machine_id", machineId);
        command.Parameters.AddWithValue("@bucket_start", bucketStartUtc);
        command.Parameters.AddWithValue("@cpu", machine.CpuPercent);
        command.Parameters.AddWithValue("@ram_used", machine.RamUsedBytes);
        command.Parameters.AddWithValue("@ram_total", machine.RamTotalBytes);
        command.Parameters.AddWithValue("@drive_used", driveUsedBytes);
        command.Parameters.AddWithValue("@drive_total", driveTotalBytes);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertDrivesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long machineSampleId,
        IReadOnlyList<DriveSamplePayload> drives,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO drive_samples (machine_sample_id, name, total_bytes, used_bytes)
            VALUES (@machine_id, @name, @total, @used);
            """;
        var machineParam = command.Parameters.Add("@machine_id", NpgsqlTypes.NpgsqlDbType.Bigint);
        var nameParam = command.Parameters.Add("@name", NpgsqlTypes.NpgsqlDbType.Text);
        var totalParam = command.Parameters.Add("@total", NpgsqlTypes.NpgsqlDbType.Bigint);
        var usedParam = command.Parameters.Add("@used", NpgsqlTypes.NpgsqlDbType.Bigint);

        foreach (var drive in drives)
        {
            machineParam.Value = machineSampleId;
            nameParam.Value = drive.Name;
            totalParam.Value = drive.TotalBytes;
            usedParam.Value = drive.UsedBytes;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertProcessesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long machineSampleId,
        IReadOnlyList<ProcessSamplePayload> processes,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO process_samples
                (machine_sample_id, process_id, process_name, cpu_percent, ram_bytes)
            VALUES
                (@machine_id, @pid, @name, @cpu, @ram);
            """;
        var machineParam = command.Parameters.Add("@machine_id", NpgsqlTypes.NpgsqlDbType.Bigint);
        var pidParam = command.Parameters.Add("@pid", NpgsqlTypes.NpgsqlDbType.Integer);
        var nameParam = command.Parameters.Add("@name", NpgsqlTypes.NpgsqlDbType.Text);
        var cpuParam = command.Parameters.Add("@cpu", NpgsqlTypes.NpgsqlDbType.Double);
        var ramParam = command.Parameters.Add("@ram", NpgsqlTypes.NpgsqlDbType.Bigint);

        foreach (var process in processes)
        {
            machineParam.Value = machineSampleId;
            pidParam.Value = process.ProcessId;
            nameParam.Value = process.ProcessName;
            cpuParam.Value = process.CpuPercent;
            ramParam.Value = process.RamBytes;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<long?> GetLatestSampleIdAsync(
        NpgsqlConnection connection,
        string machineName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ms.id
            FROM machines m
            JOIN machine_samples ms ON ms.machine_id = m.id
            WHERE m.name = @name
            ORDER BY ms.timestamp_utc DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@name", machineName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null ? null : Convert.ToInt64(result);
    }

    private static async Task<IReadOnlyList<DriveSnapshotDto>> GetDrivesAsync(
        NpgsqlConnection connection,
        long machineSampleId,
        CancellationToken cancellationToken)
    {
        var drives = new List<DriveSnapshotDto>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name, total_bytes, used_bytes
            FROM drive_samples
            WHERE machine_sample_id = @sample_id;
            """;
        command.Parameters.AddWithValue("@sample_id", machineSampleId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            drives.Add(new DriveSnapshotDto(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetInt64(2)));
        }

        return drives;
    }

    public async Task AddCommandAsync(string machineName, string commandType, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        
        // First ensure the machine exists
        var machineId = await UpsertMachineAsync(connection, null, machineName, DateTimeOffset.UtcNow, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO machine_commands (machine_id, command_type, status, created_at_utc, updated_at_utc)
            VALUES (@machine_id, @type, 'pending', @now, @now);
            """;
        command.Parameters.AddWithValue("@machine_id", machineId);
        command.Parameters.AddWithValue("@type", commandType);
        command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommandResponseDto>> GetPendingCommandsAsync(string machineName, CancellationToken cancellationToken)
    {
        var results = new List<CommandResponseDto>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.id, c.command_type, c.created_at_utc
            FROM machine_commands c
            JOIN machines m ON m.id = c.machine_id
            WHERE m.name = @name AND c.status = 'pending'
            ORDER BY c.created_at_utc;
            """;
        command.Parameters.AddWithValue("@name", machineName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CommandResponseDto(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2)));
        }

        return results;
    }

    public async Task UpdateCommandStatusAsync(long commandId, string status, string? result, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE machine_commands
            SET status = @status, result = @result, updated_at_utc = @now
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", commandId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@result", (object?)result ?? DBNull.Value);
        command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CleanupOldDataAsync(int retentionDays, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        _logger.LogInformation("Cleaning up data older than {Cutoff}...", cutoff);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Delete old machine samples (cascades to drive_samples and process_samples)
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM machine_samples WHERE timestamp_utc < @cutoff;";
                command.Parameters.AddWithValue("@cutoff", cutoff);
                var deletedSamples = await command.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogInformation("Deleted {Count} old machine samples.", deletedSamples);
            }

            // 2. Delete old minute cache
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM machine_minute_cache WHERE bucket_start_utc < @cutoff;";
                command.Parameters.AddWithValue("@cutoff", cutoff);
                var deletedCache = await command.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogInformation("Deleted {Count} old cache entries.", deletedCache);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old data.");
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
