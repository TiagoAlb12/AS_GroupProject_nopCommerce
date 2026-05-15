using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Nop.Core.Domain.Orders;
using Nop.Services.Events;
using Nop.Services.Logging;
using RabbitMQ.Client;

namespace Nop.Services.Orders;

/// <summary>
/// Bridges the nopCommerce order-created integration event to RabbitMQ.
/// </summary>
public class OrderCreatedEventConsumer : IConsumer<OrderCreatedEvent>
{
    protected readonly IConfiguration _configuration;
    protected readonly ILogger _logger;

    public OrderCreatedEventConsumer(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleEventAsync(OrderCreatedEvent eventMessage)
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
                Password = _configuration["RabbitMQ:Password"] ?? "guest"
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            var body = Encoding.UTF8.GetBytes(payload);
            var properties = channel.CreateBasicProperties();
            properties.ContentType = "application/json";
            properties.DeliveryMode = 2;
            properties.MessageId = eventMessage.EventId.ToString();
            properties.Type = eventMessage.EventType;

            channel.BasicPublish(
                exchange: "nopcommerce.order.events",
                routingKey: "order.created",
                basicProperties: properties,
                body: body);

            await Task.CompletedTask;
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync("Failed to publish OrderCreatedEvent to RabbitMQ", exception);
            throw;
        }
    }
}