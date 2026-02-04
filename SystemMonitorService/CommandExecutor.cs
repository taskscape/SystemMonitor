using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace SystemMonitorService;

public sealed class CommandExecutor
{
    private readonly HttpClient _httpClient;
    private readonly MonitorSettings _settings;
    private readonly ILogger<CommandExecutor> _logger;
    private readonly string _machineName = Environment.MachineName;

    public CommandExecutor(
        HttpClient httpClient,
        IOptions<MonitorSettings> options,
        ILogger<CommandExecutor> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task ProcessCommandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = _settings.CollectorEndpoint.Replace("/api/v1/metrics", "");
            var url = $"{baseUrl}/api/v1/machines/{Uri.EscapeDataString(_machineName)}/commands/pending";

            var commands = await _httpClient.GetFromJsonAsync<List<CommandResponse>>(url, cancellationToken);
            if (commands == null || commands.Count == 0)
            {
                return;
            }

            foreach (var command in commands)
            {
                await ExecuteCommandAsync(command, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to poll for commands.");
        }
    }

    private async Task ExecuteCommandAsync(CommandResponse command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing command {CommandType} (ID: {Id})", command.CommandType, command.Id);

        try
        {
            await UpdateStatusAsync(command.Id, "executing", null, cancellationToken);

            if (command.CommandType.Equals("restart", StringComparison.OrdinalIgnoreCase))
            {
                // First inform the server that we accepted the command
                await UpdateStatusAsync(command.Id, "completed", "Restart initiated", cancellationToken);
                
                // Then restart with a slight delay so the HTTP request has time to go out
                RestartSystem();
            }
            else
            {
                await UpdateStatusAsync(command.Id, "failed", $"Unknown command type: {command.CommandType}", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command {Id}", command.Id);
            // This might throw an exception if the network is already down, but we try anyway
            try { await UpdateStatusAsync(command.Id, "failed", ex.Message, cancellationToken); } catch { }
        }
    }

    private async Task UpdateStatusAsync(long commandId, string status, string? result, CancellationToken cancellationToken)
    {
        var baseUrl = _settings.CollectorEndpoint.Replace("/api/v1/metrics", "");
        var url = $"{baseUrl}/api/v1/commands/{commandId}/status";

        await _httpClient.PostAsJsonAsync(url, new CommandStatusUpdate(status, result), cancellationToken);
    }

    private void RestartSystem()
    {
        _logger.LogWarning("SYSTEM RESTART INITIATED BY REMOTE COMMAND");
        
        // /t 5 - wait 5 seconds (gives time for log flush and closing HTTP connections)
        var psi = new ProcessStartInfo("shutdown", "/r /t 5 /f")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
    }
}
