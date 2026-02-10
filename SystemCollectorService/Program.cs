using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;
using SystemCollectorService;

// Ensure configuration files are found when running as Windows Service
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService();

builder.Services.Configure<CollectorSettings>(
    builder.Configuration.GetSection(CollectorSettings.SectionName));

// Resolve relative SQLite path to CommonApplicationData
builder.Services.PostConfigure<CollectorSettings>(settings =>
{
    var connBuilder = new SqliteConnectionStringBuilder(settings.ConnectionString);
    if (!string.IsNullOrEmpty(connBuilder.DataSource) && 
        !Path.IsPathRooted(connBuilder.DataSource) && 
        connBuilder.DataSource != ":memory:")
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var folder = Path.Combine(appData, "SystemMonitor");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        connBuilder.DataSource = Path.Combine(folder, connBuilder.DataSource);
        settings.ConnectionString = connBuilder.ToString();
    }
});

builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<CollectorRepository>();
builder.Services.AddHostedService<StartupService>();
builder.Services.AddHostedService<DatabaseCleanupService>();

var listenUrl = builder.Configuration.GetValue<string>($"{CollectorSettings.SectionName}:ListenUrl")
    ?? "https://0.0.0.0:5101";

builder.WebHost.UseUrls(listenUrl);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/v1/metrics", async (
    List<MetricsPayload> payload,
    CollectorRepository repository,
    CancellationToken cancellationToken) =>
{
    if (payload.Count == 0)
    {
        return Results.BadRequest("Payload is empty.");
    }

    // Write directly to SQLite instead of RabbitMQ
    await repository.StoreBatchAsync(payload, cancellationToken);
    
    return Results.Accepted();
});

app.MapGet("/api/v1/machines", async (
    CollectorRepository repository,
    CancellationToken cancellationToken) =>
{
    var machines = await repository.GetMachinesAsync(cancellationToken);
    return Results.Ok(machines);
});

app.MapGet("/api/v1/machines/{machineName}/current", async (
    string machineName,
    CollectorRepository repository,
    CancellationToken cancellationToken) =>
{
    var current = await repository.GetCurrentAsync(machineName, cancellationToken);
    return current is null ? Results.NotFound() : Results.Ok(current);
});

app.MapGet("/api/v1/machines/{machineName}/history", async (
    string machineName,
    int? days,
    CollectorRepository repository,
    CancellationToken cancellationToken) =>
{
    var historyDays = days.GetValueOrDefault(7);
    if (historyDays <= 0)
    {
        return Results.BadRequest("Days must be positive.");
    }

    var history = await repository.GetHistoryAsync(machineName, historyDays, cancellationToken);
    return history.Count == 0 ? Results.NotFound() : Results.Ok(history);
});

// Endpoints for commands
app.MapPost("/api/v1/machines/{machineName}/commands", async (
    string machineName,
    CommandRequestDto request,
    CollectorRepository repository,
    CancellationToken cancellationToken) =>
{
    await repository.AddCommandAsync(machineName, request.CommandType, cancellationToken);
    return Results.Accepted();
});

app.MapGet("/api/v1/machines/{machineName}/commands/pending", async (
    string machineName,
    CollectorRepository repository,
    CancellationToken cancellationToken) =>
{
    var commands = await repository.GetPendingCommandsAsync(machineName, cancellationToken);
    return Results.Ok(commands);
});

app.MapPost("/api/v1/commands/{commandId}/status", async (
    long commandId,
    CommandStatusUpdateDto update,
    CollectorRepository repository,
    CancellationToken cancellationToken) =>
{
    await repository.UpdateCommandStatusAsync(commandId, update.Status, update.Result, cancellationToken);
    return Results.Ok();
});

app.Run();