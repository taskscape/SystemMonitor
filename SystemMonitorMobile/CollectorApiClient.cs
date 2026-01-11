using System.Net.Http.Json;

namespace SystemMonitorMobile;

public sealed class CollectorApiClient
{
    private readonly HttpClient _httpClient;
    private readonly CollectorSettings _settings;

    public CollectorApiClient(HttpClient httpClient, CollectorSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<IReadOnlyList<MachineSummaryDto>> GetMachinesAsync(CancellationToken cancellationToken)
    {
        var url = new Uri(new Uri(_settings.BaseUrl), "/api/v1/machines");
        var machines = await _httpClient.GetFromJsonAsync<List<MachineSummaryDto>>(url, cancellationToken);
        return machines ?? new List<MachineSummaryDto>();
    }

    public async Task<MachineCurrentDto?> GetCurrentAsync(string machineName, CancellationToken cancellationToken)
    {
        var url = new Uri(new Uri(_settings.BaseUrl), $"/api/v1/machines/{Uri.EscapeDataString(machineName)}/current");
        return await _httpClient.GetFromJsonAsync<MachineCurrentDto>(url, cancellationToken);
    }

    public async Task<IReadOnlyList<HistoryPointDto>> GetHistoryAsync(
        string machineName,
        int days,
        CancellationToken cancellationToken)
    {
        var url = new Uri(new Uri(_settings.BaseUrl), $"/api/v1/machines/{Uri.EscapeDataString(machineName)}/history?days={days}");
        var points = await _httpClient.GetFromJsonAsync<List<HistoryPointDto>>(url, cancellationToken);
        return points ?? new List<HistoryPointDto>();
    }
}
