using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SystemCollectorService;

public sealed class MetricsConsumer : BackgroundService
{
    private readonly ILogger<MetricsConsumer> _logger;
    private readonly ConnectionFactory _factory;
    private readonly IServiceProvider _serviceProvider;
    private const string QueueName = "metrics_queue";
    private const int BatchSize = 100; 
    private readonly TimeSpan BatchTimeout = TimeSpan.FromSeconds(1);

    public MetricsConsumer(
        IOptions<CollectorSettings> settings,
        ILogger<MetricsConsumer> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _factory = new ConnectionFactory { HostName = settings.Value.RabbitMqHostName };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = await _factory.CreateConnectionAsync(stoppingToken);
                using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken);

                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 200, global: false, cancellationToken: stoppingToken);

                _logger.LogInformation("Connected to RabbitMQ with Batch Processing (Size: {BatchSize}, Timeout: {Timeout}s)", BatchSize, BatchTimeout.TotalSeconds);

                var consumer = new AsyncEventingBasicConsumer(channel);
                var buffer = System.Threading.Channels.Channel.CreateBounded<(List<MetricsPayload> Payloads, ulong DeliveryTag)>(new System.Threading.Channels.BoundedChannelOptions(BatchSize * 4)
                {
                    FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
                });

                consumer.ReceivedAsync += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var json = Encoding.UTF8.GetString(body);
                        var payloads = JsonSerializer.Deserialize<List<MetricsPayload>>(json);

                        if (payloads is not null && payloads.Count > 0)
                        {
                            await buffer.Writer.WriteAsync((payloads, ea.DeliveryTag), stoppingToken);
                        }
                        else
                        {
                            await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deserializing message.");
                        await channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                var batch = new List<MetricsPayload>(BatchSize);
                var tags = new HashSet<ulong>();

                while (!stoppingToken.IsCancellationRequested)
                {
                    var readCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    readCts.CancelAfter(BatchTimeout);

                    try
                    {
                        while (batch.Count < BatchSize)
                        {
                            var item = await buffer.Reader.ReadAsync(readCts.Token);
                            batch.AddRange(item.Payloads);
                            tags.Add(item.DeliveryTag);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    if (batch.Count > 0)
                    {
                        await FlushBatchAsync(batch, tags, channel, stoppingToken);
                        batch.Clear();
                        tags.Clear();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ consumer loop failed. Retrying in 5s...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task FlushBatchAsync(List<MetricsPayload> batch, HashSet<ulong> tags, IChannel channel, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<CollectorRepository>();

            await repository.StoreBatchAsync(batch, cancellationToken);

            foreach (var tag in tags)
            {
                await channel.BasicAckAsync(tag, false, cancellationToken);
            }

            _logger.LogInformation("Flushed batch of {Count} metrics.", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush batch to DB. Requeuing messages.");
            foreach (var tag in tags)
            {
                await channel.BasicNackAsync(tag, false, true, cancellationToken);
            }
        }
    }
}
