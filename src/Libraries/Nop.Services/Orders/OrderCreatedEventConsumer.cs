using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nop.Core.Domain.Orders;
using Nop.Core.Telemetry;
using Nop.Services.Logging;
using RabbitMQ.Client;

namespace Nop.Services.Orders;

/// <summary>
/// Publishes nopCommerce order-created integration events to RabbitMQ.
/// </summary>
public class OrderCreatedRabbitMqPublisher
{
    protected readonly IConfiguration _configuration;
    protected readonly Nop.Services.Logging.ILogger _logger;
    private readonly Microsoft.Extensions.Logging.ILogger<OrderCreatedRabbitMqPublisher> _dotnetLogger;

    public OrderCreatedRabbitMqPublisher(
        IConfiguration configuration,
        Nop.Services.Logging.ILogger logger,
        Microsoft.Extensions.Logging.ILogger<OrderCreatedRabbitMqPublisher> dotnetLogger)
    {
        _configuration = configuration;
        _logger = logger;
        _dotnetLogger = dotnetLogger;
    }

    public async Task PublishAsync(OrderCreatedEvent eventMessage)
    {
        if (eventMessage is null)
            return;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                eventId = eventMessage.EventId,
                eventType = eventMessage.EventType,
                orderId = eventMessage.OrderId,
                customerId = eventMessage.CustomerId,
                createdAt = eventMessage.CreatedAt,
                total = eventMessage.Total
            });

            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
                Port = int.TryParse(_configuration["RabbitMQ:Port"], out var port) ? port : 5672,
                VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/",
                UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest",
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
                SocketReadTimeout = TimeSpan.FromSeconds(5),
                SocketWriteTimeout = TimeSpan.FromSeconds(5),
                AutomaticRecoveryEnabled = false
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.ConfirmSelect();

            var body = Encoding.UTF8.GetBytes(payload);

            var properties = channel.CreateBasicProperties();
            properties.ContentType = "application/json";
            properties.DeliveryMode = 2;
            properties.MessageId = eventMessage.EventId.ToString();
            properties.Type = eventMessage.EventType;

            _dotnetLogger.LogInformation(
                "Publishing OrderCreated event to RabbitMQ. order_id={OrderId} event_id={EventId}",
                eventMessage.OrderId,
                eventMessage.EventId);

            channel.BasicPublish(
                exchange: "nopcommerce.order.events",
                routingKey: "order.created",
                mandatory: true,
                basicProperties: properties,
                body: body);

            if (!channel.WaitForConfirms(TimeSpan.FromSeconds(5)))
                throw new Exception("RabbitMQ did not confirm message publish");

            TelemetryMetrics.OrderEventsPublished.Add(1);

            await _logger.InformationAsync("OrderCreatedEvent published to RabbitMQ");
            _dotnetLogger.LogInformation(
                "OrderCreated event published to RabbitMQ. order_id={OrderId} event_id={EventId}",
                eventMessage.OrderId,
                eventMessage.EventId);
        }
        catch (Exception exception)
        {
            TelemetryMetrics.OrderEventsPublishFailed.Add(1);
            await _logger.ErrorAsync("Failed to publish OrderCreatedEvent to RabbitMQ", exception);
            _dotnetLogger.LogError(
                exception,
                "Failed to publish OrderCreated event to RabbitMQ. order_id={OrderId} event_id={EventId}",
                eventMessage.OrderId,
                eventMessage.EventId);
            throw;
        }
    }
}
