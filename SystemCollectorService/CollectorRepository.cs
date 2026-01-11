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

            if (sample.Drives.Count > 0)
            {
                await InsertDrivesAsync(connection, transaction, machineSampleId, sample.Drives, cancellationToken);
            }

            if (sample.Processes.Count > 0)
            {
                await InsertProcessesAsync(connection, transaction, machineSampleId, sample.Processes, cancellationToken);
            }
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

        await using var sampleCommand = connection.CreateCommand();
        sampleCommand.CommandText = """
            SELECT m.name, ms.id, ms.timestamp_utc, ms.cpu_percent, ms.ram_used_bytes, ms.ram_total_bytes
            FROM machines m
            JOIN machine_samples ms ON ms.machine_id = m.id
            WHERE m.name = $name
            ORDER BY ms.timestamp_utc DESC
            LIMIT 1;
            """;
        sampleCommand.Parameters.AddWithValue("$name", machineName);

        await using var reader = await sampleCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var sampleId = reader.GetInt64(1);
        var dto = new MachineCurrentDto(
            reader.GetString(0),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.GetDouble(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            Array.Empty<DriveSnapshotDto>());

        await reader.CloseAsync();

        var drives = await GetDrivesAsync(connection, sampleId, cancellationToken);
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
            SELECT ms.timestamp_utc,
                   ms.cpu_percent,
                   ms.ram_used_bytes,
                   ms.ram_total_bytes,
                   COALESCE(SUM(ds.used_bytes), 0) AS drive_used,
                   COALESCE(SUM(ds.total_bytes), 0) AS drive_total
            FROM machines m
            JOIN machine_samples ms ON ms.machine_id = m.id
            LEFT JOIN drive_samples ds ON ds.machine_sample_id = ms.id
            WHERE m.name = $name AND ms.timestamp_utc >= $cutoff
            GROUP BY ms.id
            ORDER BY ms.timestamp_utc;
            """;
        command.Parameters.AddWithValue("$name", machineName);
        command.Parameters.AddWithValue("$cutoff", cutoff);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new HistoryPointDto(
                reader.GetFieldValue<DateTimeOffset>(0),
                reader.GetDouble(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.GetInt64(5)));
        }

        return results;
    }

    private static async Task<int> UpsertMachineAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string machineName,
        DateTimeOffset lastSeenUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO machines (name, first_seen_utc, last_seen_utc)
            VALUES ($name, $seen, $seen)
            ON CONFLICT (name) DO UPDATE SET last_seen_utc = EXCLUDED.last_seen_utc
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$name", machineName);
        command.Parameters.AddWithValue("$seen", lastSeenUtc);

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
                ($machine_id, $timestamp, $cpu, $ram_used, $ram_total)
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$machine_id", machineId);
        command.Parameters.AddWithValue("$timestamp", machine.TimestampUtc);
        command.Parameters.AddWithValue("$cpu", machine.CpuPercent);
        command.Parameters.AddWithValue("$ram_used", machine.RamUsedBytes);
        command.Parameters.AddWithValue("$ram_total", machine.RamTotalBytes);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
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
            VALUES ($machine_id, $name, $total, $used);
            """;
        var machineParam = command.Parameters.Add("$machine_id", NpgsqlTypes.NpgsqlDbType.Bigint);
        var nameParam = command.Parameters.Add("$name", NpgsqlTypes.NpgsqlDbType.Text);
        var totalParam = command.Parameters.Add("$total", NpgsqlTypes.NpgsqlDbType.Bigint);
        var usedParam = command.Parameters.Add("$used", NpgsqlTypes.NpgsqlDbType.Bigint);

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
                ($machine_id, $pid, $name, $cpu, $ram);
            """;
        var machineParam = command.Parameters.Add("$machine_id", NpgsqlTypes.NpgsqlDbType.Bigint);
        var pidParam = command.Parameters.Add("$pid", NpgsqlTypes.NpgsqlDbType.Integer);
        var nameParam = command.Parameters.Add("$name", NpgsqlTypes.NpgsqlDbType.Text);
        var cpuParam = command.Parameters.Add("$cpu", NpgsqlTypes.NpgsqlDbType.Double);
        var ramParam = command.Parameters.Add("$ram", NpgsqlTypes.NpgsqlDbType.Bigint);

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
            WHERE machine_sample_id = $sample_id;
            """;
        command.Parameters.AddWithValue("$sample_id", machineSampleId);

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
}
