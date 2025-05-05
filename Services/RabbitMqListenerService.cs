using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using WasteCollection.Api.Data;
using WasteCollection.Api.Models;

namespace WasteCollection.Api.Services;

public class RabbitMqListenerService : IHostedService, IDisposable
{
    private readonly ILogger<RabbitMqListenerService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IChannel? _channel;
    private const string QueueName = "request_processing_queue";

    public RabbitMqListenerService(
        ILogger<RabbitMqListenerService> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        try
        {
            var rabbitMqConfig = configuration.GetSection("RabbitMQ");
            _factory = new ConnectionFactory()
            {
                HostName = rabbitMqConfig["HostName"] ?? "localhost",
                Port = int.TryParse(rabbitMqConfig["Port"], out var port) ? port : 5672,
                UserName = rabbitMqConfig["UserName"] ?? "guest",
                Password = rabbitMqConfig["Password"] ?? "guest",
                VirtualHost = rabbitMqConfig["VirtualHost"] ?? "/",
                ConsumerDispatchConcurrency = 1
            };
            _logger.LogInformation("RabbitMQ Listener Service configured for Host: {HostName}", _factory.HostName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring RabbitMQ Connection Factory for Listener Service.");
            throw;
        }
    }
    public async Task StartAsync(CancellationToken cancellationToken) 
    {
        _logger.LogInformation("RabbitMQ Listener Service starting.");

        try
        {
            _connection = await _factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync();

            if (_channel == null)
            {
                _logger.LogError("Failed to create RabbitMQ channel.");
                return;
            }

            await _channel.QueueDeclareAsync(queue: QueueName,
                                        durable: true,
                                        exclusive: false,
                                        autoDelete: false,
                                        arguments: null);

            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation("RabbitMQ Listener Service waiting for messages on queue '{QueueName}'.", QueueName);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            var serviceProvider = _serviceProvider;
            var logger = _logger;
            var channelRef = _channel;


            consumer.ReceivedAsync += async (model, ea) =>
            {
                if (channelRef == null)
                {
                    logger.LogError("RabbitMQ channel is not available during message processing.");
                    return;
                }

                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                logger.LogInformation("Received message: {Message}", message);

                int requestId;
                if (!int.TryParse(message, out requestId))
                {
                    logger.LogError("Could not parse Request ID from message: {Message}. Acknowledging to discard.", message);
                    await channelRef.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                    return;
                }

                try
                {
                    await ProcessRequestStaticAsync(requestId, serviceProvider, logger, cancellationToken);

                    await channelRef.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                    logger.LogInformation("Acknowledged message for Request ID: {RequestId}", requestId);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Processing cancelled for Request ID: {RequestId}.", requestId);
                    await channelRef.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing message for Request ID: {RequestId}. Message will be negatively acknowledged.", requestId);
                    await channelRef.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            await _channel.BasicConsumeAsync(queue: QueueName,
                                         autoAck: false,
                                         consumer: consumer);

        }
        catch (OperationCanceledException)
        {
             _logger.LogInformation("RabbitMQ Listener Service startup cancelled.");
             throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting RabbitMQ Listener Service.");
        }
    }

    private static async Task ProcessRequestStaticAsync(
        int requestId,
        IServiceProvider serviceProvider,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing request ID: {RequestId}...", requestId);

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var request = await dbContext.Requests.FindAsync(new object[] { requestId }, cancellationToken);

            if (request == null)
            {
                logger.LogWarning("Request ID: {RequestId} not found in database. Skipping processing.", requestId);
                return;
            }

            if (request.Status != RequestStatus.Pending)
            {
                logger.LogWarning("Request ID: {RequestId} is already in status {Status}. Skipping processing.", requestId, request.Status);
                return;
            }

            request.Status = RequestStatus.Processing;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Request ID: {RequestId} status updated to Processing.", requestId);

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            request = await dbContext.Requests.FindAsync(new object[] { requestId }, cancellationToken);
            if (request == null || request.Status != RequestStatus.Processing)
            {
                 logger.LogWarning("Request ID: {RequestId} state changed during processing delay or not found. Aborting completion.", requestId);
                 return;
            }

            request.Status = RequestStatus.Completed;
            request.ProcessedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Request ID: {RequestId} processing completed.", requestId);
        }
    }


    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RabbitMQ Listener Service stopping.");

        if (_channel?.IsOpen ?? false)
        {
             await _channel.CloseAsync();
            _logger.LogInformation("RabbitMQ channel closed.");
        }
        if (_connection?.IsOpen ?? false)
        {
             await _connection.CloseAsync();
            _logger.LogInformation("RabbitMQ connection closed.");
        }
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing RabbitMQ Listener Service.");
        _channel?.Dispose();
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}
