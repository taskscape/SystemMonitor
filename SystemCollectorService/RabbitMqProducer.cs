using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace SystemCollectorService;

public interface IMetricsProducer
{
    Task PublishMetricsAsync(List<MetricsPayload> payload, CancellationToken cancellationToken);
}

public sealed class RabbitMqProducer : IMetricsProducer, IDisposable
{
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IChannel? _channel;
    private const string QueueName = "metrics_queue";
    private readonly ILogger<RabbitMqProducer> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RabbitMqProducer(IOptions<CollectorSettings> settings, ILogger<RabbitMqProducer> logger)
    {
        _logger = logger;
        _factory = new ConnectionFactory { HostName = settings.Value.RabbitMqHostName };
    }

    public async Task PublishMetricsAsync(List<MetricsPayload> payload, CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(cancellationToken);

        if (_channel is null)
        {
             _logger.LogError("Channel is null, cannot publish.");
             return;
        }

        var json = JsonSerializer.Serialize(payload);
        var body = Encoding.UTF8.GetBytes(json);

        await _channel.BasicPublishAsync(exchange: string.Empty, routingKey: QueueName, body: body, cancellationToken: cancellationToken);
    }

    private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null && _channel.IsOpen)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is not null && _channel.IsOpen)
            {
                return;
            }

            _connection = await _factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            
            await _channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _lock.Dispose();
    }
}
