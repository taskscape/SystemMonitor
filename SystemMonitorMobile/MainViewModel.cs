using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SystemMonitorMobile;

public sealed class MainViewModel : BindableBase
{
    private readonly CollectorApiClient _apiClient;
    private bool _isBusy;
    private string _statusMessage = "Idle";
    private MachineSummaryDto? _selectedMachine;
    private double _cpuPercent;
    private double _ramPercent;
    private double _drivePercent;
    private string _lastSeen = "--";
    private IReadOnlyList<DriveDisplay> _drives = Array.Empty<DriveDisplay>();

    public ObservableCollection<MachineSummaryDto> Machines { get; } = new();
    public ObservableCollection<HistoryDisplayPoint> HistoryPoints { get; } = new();

    public MainViewModel(CollectorApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new Command(async () => await RefreshAsync());
    }

    public ICommand RefreshCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public MachineSummaryDto? SelectedMachine
    {
        get => _selectedMachine;
        set
        {
            if (SetSelectedMachine(value))
            {
                _ = LoadSelectionAsync();
            }
        }
    }

    public double CpuPercent
    {
        get => _cpuPercent;
        set => SetProperty(ref _cpuPercent, value);
    }

    public double RamPercent
    {
        get => _ramPercent;
        set => SetProperty(ref _ramPercent, value);
    }

    public double DrivePercent
    {
        get => _drivePercent;
        set => SetProperty(ref _drivePercent, value);
    }

    public string LastSeen
    {
        get => _lastSeen;
        set => SetProperty(ref _lastSeen, value);
    }

    public IReadOnlyList<DriveDisplay> Drives
    {
        get => _drives;
        set
        {
            SetProperty(ref _drives, value);
            RaisePropertyChanged(nameof(HasDrives));
        }
    }

    public bool HasDrives => Drives.Count > 0;

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    private bool SetSelectedMachine(MachineSummaryDto? value)
    {
        if (ReferenceEquals(_selectedMachine, value))
        {
            return false;
        }

        _selectedMachine = value;
        RaisePropertyChanged(nameof(SelectedMachine));
        return true;
    }

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Syncing";

            var machines = await _apiClient.GetMachinesAsync(CancellationToken.None);
            Machines.Clear();
            foreach (var machine in machines)
            {
                Machines.Add(machine);
            }

            SelectedMachine = Machines.Count > 0 ? Machines[0] : null;
            if (SelectedMachine is null)
            {
                ResetDashboard();
            }

            StatusMessage = "Ready";
        }
        catch (Exception)
        {
            StatusMessage = "Offline";
            ResetDashboard();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSelectionAsync()
    {
        if (SelectedMachine is null)
        {
            ResetDashboard();
            return;
        }

        try
        {
            StatusMessage = "Loading";

            var currentTask = _apiClient.GetCurrentAsync(SelectedMachine.MachineName, CancellationToken.None);
            var historyTask = _apiClient.GetHistoryAsync(SelectedMachine.MachineName, 7, CancellationToken.None);
            await Task.WhenAll(currentTask, historyTask);

            var current = currentTask.Result;
            var history = historyTask.Result;

            if (current is null)
            {
                ResetDashboard();
                StatusMessage = "No data";
                return;
            }

            CpuPercent = current.CpuPercent;
            RamPercent = Percent(current.RamUsedBytes, current.RamTotalBytes);

            var driveTotals = current.Drives.Aggregate(
                (used: 0d, total: 0d),
                (acc, drive) => (acc.used + drive.UsedBytes, acc.total + drive.TotalBytes));
            DrivePercent = Percent(driveTotals.used, driveTotals.total);
            LastSeen = current.TimestampUtc.ToString("u");

            Drives = current.Drives.Select(drive => new DriveDisplay
            {
                Name = drive.Name,
                UsedBytes = drive.UsedBytes,
                TotalBytes = drive.TotalBytes
            }).ToList();

            HistoryPoints.Clear();
            foreach (var point in history)
            {
                HistoryPoints.Add(new HistoryDisplayPoint
                {
                    TimestampUtc = point.TimestampUtc,
                    CpuPercent = point.CpuPercent,
                    RamPercent = Percent(point.RamUsedBytes, point.RamTotalBytes),
                    DrivePercent = Percent(point.DriveUsedBytes, point.DriveTotalBytes)
                });
            }

            StatusMessage = "Ready";
        }
        catch (Exception)
        {
            StatusMessage = "Unavailable";
            ResetDashboard();
        }
    }

    private void ResetDashboard()
    {
        CpuPercent = 0;
        RamPercent = 0;
        DrivePercent = 0;
        LastSeen = "--";
        Drives = Array.Empty<DriveDisplay>();
        HistoryPoints.Clear();
    }

    private static double Percent(double used, double total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return used / total * 100d;
    }
}
