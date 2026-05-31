using System.Text;
using System.Text.Json;
using OrderIntegrationService.Models;
using OrderIntegrationService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderIntegrationService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly MockStockService _stockService;
    private readonly MockShippingService _shippingService;

    private IConnection? _connection;
    private IModel? _channel;

    public Worker(
        ILogger<Worker> logger,
        IConfiguration configuration,
        MockStockService stockService,
        MockShippingService shippingService)
    {
        _logger = logger;
        _configuration = configuration;
        _stockService = stockService;
        _shippingService = shippingService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
            Port = int.TryParse(_configuration["RabbitMQ:Port"], out var port) ? port : 5672,
            VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/",
            UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        var queueName = _configuration["RabbitMQ:QueueName"] ?? "nopcommerce.order.created";

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (_, ea) =>
        {
            var body = ea.Body.ToArray();
            var payload = Encoding.UTF8.GetString(body);

            try
            {
                _logger.LogInformation("Received OrderCreated event: {Payload}", payload);

                var order = JsonSerializer.Deserialize<OrderCreatedMessage>(
                    payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (order is null)
                    throw new Exception("Invalid OrderCreated payload");

                await _stockService.ReserveStockAsync(order);
                await _shippingService.CreateShipmentAsync(order);

                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

                _logger.LogInformation(
                    "OrderCreated event processed and acknowledged. OrderId={OrderId}",
                    order.OrderId);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to process OrderCreated event");

                _channel.BasicNack(
                    deliveryTag: ea.DeliveryTag,
                    multiple: false,
                    requeue: false);
            }
        };

        _channel.BasicConsume(
            queue: queueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("Order Integration Service started. Listening queue: {QueueName}", queueName);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();

        base.Dispose();
    }
}