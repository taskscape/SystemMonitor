using System.Net.Http.Json;
using System.Text.Json;

namespace SystemMonitorMobile;

public sealed class CollectorApiClient
{
    private readonly HttpClient _httpClient;
    private readonly CollectorSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    public CollectorApiClient(HttpClient httpClient, CollectorSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<IReadOnlyList<MachineSummaryDto>> GetMachinesAsync(CancellationToken cancellationToken)
    {
        var url = new Uri(new Uri(_settings.BaseUrl), "/api/v1/machines");
        var machines = await _httpClient.GetFromJsonAsync<List<MachineSummaryDto>>(url, _jsonOptions, cancellationToken);
        return machines ?? new List<MachineSummaryDto>();
    }

    public async Task<MachineCurrentDto?> GetCurrentAsync(string machineName, CancellationToken cancellationToken)
    {
        var url = new Uri(new Uri(_settings.BaseUrl), $"/api/v1/machines/{Uri.EscapeDataString(machineName)}/current");
        return await _httpClient.GetFromJsonAsync<MachineCurrentDto>(url, _jsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<HistoryPointDto>> GetHistoryAsync(
        string machineName,
        int days,
        CancellationToken cancellationToken)
    {
        var url = new Uri(new Uri(_settings.BaseUrl), $"/api/v1/machines/{Uri.EscapeDataString(machineName)}/history?days={days}");
        var points = await _httpClient.GetFromJsonAsync<List<HistoryPointDto>>(url, _jsonOptions, cancellationToken);
        return points ?? new List<HistoryPointDto>();
    }
}
