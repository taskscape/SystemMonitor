using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SystemMonitorMobile;

public sealed class CollectorSettings
{
    public string BaseUrl { get; set; } = "http://localhost:5100";
}

public sealed class MachineSummaryDto
{
    [JsonPropertyName("machineName")]
    public string MachineName { get; set; } = string.Empty;

    [JsonPropertyName("lastSeenUtc")]
    public DateTimeOffset LastSeenUtc { get; set; }
}

public sealed class MachineCurrentDto
{
    [JsonPropertyName("machineName")]
    public string MachineName { get; set; } = string.Empty;

    [JsonPropertyName("timestampUtc")]
    public DateTimeOffset TimestampUtc { get; set; }

    [JsonPropertyName("cpuPercent")]
    public double CpuPercent { get; set; }

    [JsonPropertyName("ramUsedBytes")]
    public double RamUsedBytes { get; set; }

    [JsonPropertyName("ramTotalBytes")]
    public double RamTotalBytes { get; set; }

    [JsonPropertyName("drives")]
    public List<DriveSnapshotDto> Drives { get; set; } = new();
}

public sealed class DriveSnapshotDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("totalBytes")]
    public double TotalBytes { get; set; }

    [JsonPropertyName("usedBytes")]
    public double UsedBytes { get; set; }
}

public sealed class HistoryPointDto
{
    [JsonPropertyName("timestampUtc")]
    public DateTimeOffset TimestampUtc { get; set; }

    [JsonPropertyName("cpuPercent")]
    public double CpuPercent { get; set; }

    [JsonPropertyName("ramUsedBytes")]
    public double RamUsedBytes { get; set; }

    [JsonPropertyName("ramTotalBytes")]
    public double RamTotalBytes { get; set; }

    [JsonPropertyName("driveUsedBytes")]
    public double DriveUsedBytes { get; set; }

    [JsonPropertyName("driveTotalBytes")]
    public double DriveTotalBytes { get; set; }
}

public sealed class HistoryDisplayPoint
{
    public DateTimeOffset TimestampUtc { get; init; }
    public double CpuPercent { get; init; }
    public double RamPercent { get; init; }
    public double DrivePercent { get; init; }
}

public sealed class DriveDisplay
{
    public string Name { get; init; } = string.Empty;
    public double UsedBytes { get; init; }
    public double TotalBytes { get; init; }
}

public abstract class BindableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
