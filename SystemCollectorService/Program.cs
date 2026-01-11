using Microsoft.Extensions.Options;
using Npgsql;
using SystemCollectorService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService();

builder.Services.Configure<CollectorSettings>(
    builder.Configuration.GetSection(CollectorSettings.SectionName));

builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<CollectorRepository>();
builder.Services.AddHostedService<StartupService>();

builder.Services.AddSingleton(sp =>
{
    var settings = sp.GetRequiredService<IOptions<CollectorSettings>>().Value;
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(settings.ConnectionString);
    return dataSourceBuilder.Build();
});

var listenUrl = builder.Configuration.GetValue<string>($"{CollectorSettings.SectionName}:ListenUrl")
    ?? "http://0.0.0.0:5100";

builder.WebHost.UseUrls(listenUrl);

var app = builder.Build();

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

    await repository.StoreBatchAsync(payload, cancellationToken);
    return Results.Ok();
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

app.Run();
