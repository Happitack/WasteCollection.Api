using System.Text;
using RabbitMQ.Client;
using WasteCollection.Api.Interfaces;

namespace WasteCollection.Api.Services;

public class RabbitMqPublisher : IMessagePublisher
{
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly ConnectionFactory _factory;
    private const string QueueName = "request_processing_queue"; 

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;
        try
        {
            // Read settings from appsettings.json
            var rabbitMqConfig = configuration.GetSection("RabbitMQ");
            _factory = new ConnectionFactory()
            {
                HostName = rabbitMqConfig["HostName"] ?? "localhost",
                Port = int.TryParse(rabbitMqConfig["Port"], out var port) ? port : 5672,
                UserName = rabbitMqConfig["UserName"] ?? "guest",
                Password = rabbitMqConfig["Password"] ?? "guest",
                VirtualHost = rabbitMqConfig["VirtualHost"] ?? "/"
            };
            _logger.LogInformation("RabbitMQ Connection Factory configured for Host: {HostName}", _factory.HostName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring RabbitMQ Connection Factory.");
            throw;
        }
    }

    public async Task PublishNewRequestNotification(int requestId)
    {
        try
        {
            using var connection = await _factory.CreateConnectionAsync();

            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: QueueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            string message = requestId.ToString();
            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(exchange: "",
                                 routingKey: QueueName,
                                 basicProperties: properties,
                                 mandatory: false,
                                 body: body);

            _logger.LogInformation("Published Request ID: {RequestId} to Queue: {QueueName}", requestId, QueueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message for Request ID: {RequestId} to Queue: {QueueName}", requestId, QueueName);
        }
    }
}
