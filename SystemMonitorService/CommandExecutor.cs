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
                // Najpierw informujemy serwer, że przyjęliśmy rozkaz
                await UpdateStatusAsync(command.Id, "completed", "Restart initiated", cancellationToken);
                
                // Potem restartujemy z lekkim opóźnieniem, aby request HTTP zdążył wyjść
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
            // Tu może rzucić wyjątek, jeśli sieć już padła, ale próbujemy
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
        
        // /t 5 - czekaj 5 sekund (daje czas na flush logów i zamknięcie połączeń HTTP)
        var psi = new ProcessStartInfo("shutdown", "/r /t 5 /f")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
    }
}
