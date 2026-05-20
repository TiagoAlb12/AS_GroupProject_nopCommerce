using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Nop.Core.Domain.Orders;
using Nop.Services.Logging;
using RabbitMQ.Client;

namespace Nop.Services.Orders;

/// <summary>
/// Publishes nopCommerce order-created integration events to RabbitMQ.
/// </summary>
public class OrderCreatedRabbitMqPublisher
{
    protected readonly IConfiguration _configuration;
    protected readonly ILogger _logger;

    public OrderCreatedRabbitMqPublisher(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger;
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

            channel.BasicPublish(
                exchange: "nopcommerce.order.events",
                routingKey: "order.created",
                mandatory: true,
                basicProperties: properties,
                body: body);

            if (!channel.WaitForConfirms(TimeSpan.FromSeconds(5)))
                throw new Exception("RabbitMQ did not confirm message publish");

            await _logger.InformationAsync("OrderCreatedEvent published to RabbitMQ");
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync("Failed to publish OrderCreatedEvent to RabbitMQ", exception);
            throw;
        }
    }
}