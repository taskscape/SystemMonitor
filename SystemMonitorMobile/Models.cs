using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SystemMonitorMobile;

public sealed class CollectorSettings
{
    private const string Key = "server_url";
    private const string DefaultUrl = "https://10.0.2.2:5101";

    public string BaseUrl
    {
        get => Preferences.Get(Key, DefaultUrl);
        set => Preferences.Set(Key, value);
    }
}

public sealed class MachineSummaryDto
{
    public string MachineName { get; set; } = string.Empty;
    public DateTimeOffset LastSeenUtc { get; set; }
}

public sealed class MachineCurrentDto
{
    public string MachineName { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public double CpuPercent { get; set; }
    public double RamUsedBytes { get; set; }
    public double RamTotalBytes { get; set; }
    public List<DriveSnapshotDto> Drives { get; set; } = new();
}

public sealed class DriveSnapshotDto
{
    public string Name { get; set; } = string.Empty;
    public double TotalBytes { get; set; }
    public double UsedBytes { get; set; }
}

public sealed class HistoryPointDto
{
    public DateTimeOffset TimestampUtc { get; set; }
    public double CpuPercent { get; set; }
    public double RamUsedBytes { get; set; }
    public double RamTotalBytes { get; set; }
    public double DriveUsedBytes { get; set; }
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
