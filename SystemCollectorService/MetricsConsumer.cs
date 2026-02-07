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
    private const int BatchSize = 50; // Insert 50 payloads at once
    private readonly TimeSpan BatchTimeout = TimeSpan.FromSeconds(5); // Or every 5 seconds

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

                // QoS: Prefetch 2x BatchSize to keep the pipeline busy but not flooded
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: (ushort)(BatchSize * 2), global: false, cancellationToken: stoppingToken);

                _logger.LogInformation("Connected to RabbitMQ with Batch Processing (Size: {BatchSize}, Timeout: {Timeout}s)", BatchSize, BatchTimeout.TotalSeconds);

                var consumer = new AsyncEventingBasicConsumer(channel);
                
                // We will use a Channel to act as a local buffer between RabbitMQ callback and our DB loop
                var buffer = System.Threading.Channels.Channel.CreateBounded<(MetricsPayload Payload, ulong DeliveryTag)>(new System.Threading.Channels.BoundedChannelOptions(BatchSize * 4)
                {
                    FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
                });

                consumer.ReceivedAsync += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var json = Encoding.UTF8.GetString(body);
                        var payload = JsonSerializer.Deserialize<List<MetricsPayload>>(json);

                        if (payload is not null && payload.Count > 0)
                        {
                            // Flatten the list: our DB repository expects List<MetricsPayload>
                            // But here we might receive a List of one or more payloads per message.
                            // For simplicity, we assume the message contains a batch from Agent, 
                            // but we want to re-batch optimally for DB.
                            
                            foreach (var p in payload)
                            {
                                await buffer.Writer.WriteAsync((p, ea.DeliveryTag), stoppingToken);
                            }
                        }
                        else
                        {
                            // Empty payload, just ack
                            await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deserializing message.");
                        // Nack and discard (false) because it's likely malformed
                        await channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                // Processing Loop
                var batch = new List<MetricsPayload>(BatchSize);
                var tags = new HashSet<ulong>(); // To track delivery tags for Ack

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Read from buffer with timeout
                    var readCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    readCts.CancelAfter(BatchTimeout);

                    try
                    {
                        while (batch.Count < BatchSize)
                        {
                            // Wait for next item or timeout
                            var item = await buffer.Reader.ReadAsync(readCts.Token);
                            batch.Add(item.Payload);
                            tags.Add(item.DeliveryTag);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout reached, flush what we have
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

            // Bulk Insert to DB
            await repository.StoreBatchAsync(batch, cancellationToken);

            // Ack all messages involved in this batch
            // Safer: Loop and Ack individual tags. It's slightly more network chatter but safer logic.
            
            foreach (var tag in tags)
            {
                await channel.BasicAckAsync(tag, false, cancellationToken);
            }

            _logger.LogInformation("Flushed batch of {Count} metrics.", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush batch to DB. Requeuing messages.");
            
            // If DB fails, we must Nack so RabbitMQ redelivers them later.
            foreach (var tag in tags)
            {
                await channel.BasicNackAsync(tag, false, true, cancellationToken);
            }
        }
    }
}