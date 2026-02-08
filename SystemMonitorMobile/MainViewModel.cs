using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes; // For PointCollection
using Point = Microsoft.Maui.Graphics.Point; // For Point

namespace SystemMonitorMobile;

public sealed class MainViewModel : BindableBase
{
    private readonly CollectorApiClient _apiClient;
    private readonly CollectorSettings _settings; // Added
    
    private bool _isBusy;
    private string _statusMessage = "Idle";
    private MachineSummaryDto? _selectedMachine;
    private double _cpuPercent;
    private double _ramPercent;
    private double _drivePercent;
    private string _lastSeen = "--";
    
    // New fields for configuration
    private bool _isConfigured;
    private string _serverUrlInput = string.Empty;

    public ObservableCollection<DriveDisplay> Drives { get; } = new();
    
    private PointCollection _cpuChartPoints = new();
    private PointCollection _ramChartPoints = new();
    private PointCollection _driveChartPoints = new();

    public ObservableCollection<MachineSummaryDto> Machines { get; } = new();

    // Constructor
    public MainViewModel(CollectorApiClient apiClient, CollectorSettings settings)
    {
        _apiClient = apiClient;
        _settings = settings;
        
        // Initialize text field with current value
        ServerUrlInput = _settings.BaseUrl;
        
        RefreshCommand = new Command(async () => await RefreshAsync());
        SaveConfigCommand = new Command(SaveConfig);
        RestartCommand = new Command(async () => await RestartAsync());
        
        // Default to configuration screen on start so user can enter address
        IsConfigured = false; 
    }

    public ICommand RefreshCommand { get; }
    public ICommand SaveConfigCommand { get; }
    public ICommand RestartCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool IsConfigured
    {
        get => _isConfigured;
        set => SetProperty(ref _isConfigured, value);
    }

    public string ServerUrlInput
    {
        get => _serverUrlInput;
        set => SetProperty(ref _serverUrlInput, value);
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

    private bool _hasDrives;
    public bool HasDrives
    {
        get => _hasDrives;
        set => SetProperty(ref _hasDrives, value);
    }

    public PointCollection CpuChartPoints
    {
        get => _cpuChartPoints;
        set => SetProperty(ref _cpuChartPoints, value);
    }

    public PointCollection RamChartPoints
    {
        get => _ramChartPoints;
        set => SetProperty(ref _ramChartPoints, value);
    }

    public PointCollection DriveChartPoints
    {
        get => _driveChartPoints;
        set => SetProperty(ref _driveChartPoints, value);
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    private void SaveConfig()
    {
        if (string.IsNullOrWhiteSpace(ServerUrlInput)) return;
        
        // Save to preferences
        _settings.BaseUrl = ServerUrlInput;
        
        // Toggle view and refresh
        IsConfigured = true;
        _ = RefreshAsync();
    }
    
    // Method to be called from UI to enter configuration mode
    public void EnterConfigMode()
    {
        IsConfigured = false;
        ServerUrlInput = _settings.BaseUrl;
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
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Syncing...";

            var previousSelectionName = SelectedMachine?.MachineName;

            var machines = await _apiClient.GetMachinesAsync(CancellationToken.None);
            
            Machines.Clear();
            foreach (var machine in machines)
            {
                Machines.Add(machine);
            }

            if (!string.IsNullOrEmpty(previousSelectionName))
            {
                var match = Machines.FirstOrDefault(m => m.MachineName == previousSelectionName);
                if (match != null) SelectedMachine = match;
                else SelectedMachine = Machines.FirstOrDefault();
            }
            else
            {
                if (SelectedMachine == null && Machines.Count > 0)
                {
                    SelectedMachine = Machines[0];
                }
            }

            if (SelectedMachine == null) StatusMessage = "No machines found";
            else StatusMessage = "Ready";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Err: {ex.Message}";
            // If network error, maybe user has wrong address?
            // Optional: EnterConfigMode(); // But this might be annoying.
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RestartAsync()
    {
        try
        {
            if (SelectedMachine == null || IsBusy) return;

            var page = Application.Current?.MainPage;
            if (page == null)
            {
                StatusMessage = "Err: No UI context";
                return;
            }

            bool confirm = await page.DisplayAlert(
                "Restart Machine",
                $"Are you sure you want to restart {SelectedMachine.MachineName}?",
                "Yes", "No");

            if (!confirm) return;

            IsBusy = true;
            StatusMessage = "Sending restart command...";
            await _apiClient.RestartMachineAsync(SelectedMachine.MachineName, CancellationToken.None);
            StatusMessage = "Restart command sent!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Err: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSelectionAsync()
    {
        if (SelectedMachine is null) return;

        try
        {
            StatusMessage = "Loading...";

            var currentTask = _apiClient.GetCurrentAsync(SelectedMachine.MachineName, CancellationToken.None);
            var historyTask = _apiClient.GetHistoryAsync(SelectedMachine.MachineName, 7, CancellationToken.None);
            await Task.WhenAll(currentTask, historyTask);

            var current = currentTask.Result;
            var history = historyTask.Result;

            if (current is null)
            {
                StatusMessage = "No data";
                return;
            }

            var driveTotals = current.Drives.Aggregate(
                (used: 0d, total: 0d),
                (acc, drive) => (acc.used + drive.UsedBytes, acc.total + drive.TotalBytes));
            
            var cpuPoints = new PointCollection();
            var ramPoints = new PointCollection();
            var hddPoints = new PointCollection();

            if (history.Count > 1)
            {
                double width = 350;
                double height = 100;
                double stepX = width / (history.Count - 1);

                for (int i = 0; i < history.Count; i++)
                {
                    var pt = history[i];
                    double x = i * stepX;
                    
                    double cpuY = height - (pt.CpuPercent / 100.0 * height);
                    if (double.IsNaN(cpuY) || double.IsInfinity(cpuY)) cpuY = height;
                    cpuPoints.Add(new Point(x, Math.Clamp(cpuY, 0, height)));
                    
                    double ramP = Percent(pt.RamUsedBytes, pt.RamTotalBytes);
                    double ramY = height - (ramP / 100.0 * height);
                    if (double.IsNaN(ramY) || double.IsInfinity(ramY)) ramY = height;
                    ramPoints.Add(new Point(x, Math.Clamp(ramY, 0, height)));

                    double hddP = Percent(pt.DriveUsedBytes, pt.DriveTotalBytes);
                    double hddY = height - (hddP / 100.0 * height);
                    if (double.IsNaN(hddY) || double.IsInfinity(hddY)) hddY = height;
                    hddPoints.Add(new Point(x, Math.Clamp(hddY, 0, height)));
                }
            }
            else
            {
                var p0 = new Point(0, 100);
                var p1 = new Point(350, 100);
                cpuPoints.Add(p0); cpuPoints.Add(p1);
                ramPoints.Add(p0); ramPoints.Add(p1);
                hddPoints.Add(p0); hddPoints.Add(p1);
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                CpuPercent = current.CpuPercent;
                RamPercent = Percent(current.RamUsedBytes, current.RamTotalBytes);
                DrivePercent = Percent(driveTotals.used, driveTotals.total);
                
                LastSeen = current.TimestampUtc.ToLocalTime().ToString("HH:mm dd.MM.yyyy");

                Drives.Clear();
                foreach(var d in current.Drives)
                {
                    Drives.Add(new DriveDisplay 
                    { 
                        Name = d.Name, 
                        UsedBytes = d.UsedBytes, 
                        TotalBytes = d.TotalBytes 
                    });
                }
                HasDrives = Drives.Count > 0;

                CpuChartPoints = cpuPoints;
                RamChartPoints = ramPoints;
                DriveChartPoints = hddPoints;

                var timeDiff = DateTimeOffset.UtcNow - current.TimestampUtc;
                if (timeDiff.TotalMinutes > 2) StatusMessage = "Offline";
                else StatusMessage = "Online";
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Err: {ex.Message}";
        }
    }

    private static double Percent(double used, double total)
    {
        if (total <= 0) return 0;
        return used / total * 100d;
    }
}