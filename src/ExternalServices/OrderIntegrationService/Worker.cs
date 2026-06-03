using System.Text;
using System.Text.Json;
using OrderIntegrationService.Models;
using OrderIntegrationService.Services;
using OrderIntegrationService.Telemetry;          
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderIntegrationService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly MockStockService _stockService;
    private readonly MockShippingService _shippingService;

    private readonly OrderIntegrationStateStore _stateStore;

    private IConnection? _connection;
    private IModel? _channel;

    public Worker(
        ILogger<Worker> logger,
        IConfiguration configuration,
        MockStockService stockService,
        MockShippingService shippingService,
        OrderIntegrationStateStore stateStore)
    {
        _logger = logger;
        _configuration = configuration;
        _stockService = stockService;
        _shippingService = shippingService;
        _stateStore = stateStore;
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
            OrderCreatedMessage? order = null;

            try
            {
                _logger.LogInformation("Received OrderCreated event: {Payload}", payload);

                order = JsonSerializer.Deserialize<OrderCreatedMessage>(
                    payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (order is null)
                    throw new Exception("Invalid OrderCreated payload");

                _stateStore.SetReceived(order);

                // ── NOVO: processo com métricas e logs estruturados ──────────────
                await ProcessOrderAsync(order, _logger);

                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

                _logger.LogInformation(
                    "OrderCreated event processed and acknowledged. OrderId={OrderId}",
                    order.OrderId);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to process OrderCreated event");

                if (order is not null)
                    _stateStore.SetFailed(order.OrderId, exception.Message);

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

    private async Task ProcessOrderAsync(OrderCreatedMessage order, ILogger logger)
    {
        // ── Log estruturado: correlação order_id + event_id (assumindo que OrderCreatedMessage tem EventId e CustomerId)
        // Se a classe não tiver estas propriedades, ajuste ou comente. O código original usava OrderCreatedMessage.
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["order_id"]  = order.OrderId,
            ["event_id"]  = order.EventId,         // Adicione esta propriedade à classe OrderCreatedMessage se não existir
            ["customer_id"] = order.CustomerId     // Adicione esta propriedade se necessário
        });

        logger.LogInformation(
            "Processing OrderCreated event. order_id={OrderId} event_id={EventId} total={Total}",
            order.OrderId, order.EventId, order.Total);

        // ── Métrica: mensagem consumida (Vis 1 do lado do consumidor) ─────────────
        OrderIntegrationMetrics.MessagesConsumed.Add(1,
            new KeyValuePair<string, object?>("queue", "order.created"));

        try
        {
            // ── Stock ─────────────────────────────────────────────────────────────
            logger.LogInformation(
                "Reserving stock. order_id={OrderId} event_id={EventId}",
                order.OrderId, order.EventId);

            // O método original é ReserveStockAsync, não ReserveAsync.
            // Mantemos a chamada original.
            await _stockService.ReserveStockAsync(order);
            _stateStore.SetStockReserved(order.OrderId);

            OrderIntegrationMetrics.StockReserved.Add(1);
            logger.LogInformation(
                "Stock reserved. order_id={OrderId} event_id={EventId}",
                order.OrderId, order.EventId);

            // ── Shipping ──────────────────────────────────────────────────────────
            logger.LogInformation(
                "Creating shipment. order_id={OrderId} event_id={EventId}",
                order.OrderId, order.EventId);

            var trackingCode = await _shippingService.CreateShipmentAsync(order);
            _stateStore.SetShipmentCreated(order.OrderId, trackingCode);

            OrderIntegrationMetrics.ShipmentCreated.Add(1);
            logger.LogInformation(
                "Shipment created. order_id={OrderId} event_id={EventId} tracking={TrackingCode}",
                order.OrderId, order.EventId, trackingCode);

            _stateStore.SetCompleted(order.OrderId);

            logger.LogInformation(
                "Order processing completed. order_id={OrderId} event_id={EventId}",
                order.OrderId, order.EventId);
        }
        catch (Exception ex)
        {
            OrderIntegrationMetrics.MessagesProcessingFailed.Add(1,
                new KeyValuePair<string, object?>("queue", "order.created"));

            logger.LogError(ex,
                "Order processing failed — will be sent to DLQ. order_id={OrderId} event_id={EventId}",
                order.OrderId, order.EventId);

            throw; // mantém o comportamento original: a exceção é relançada para o catch externo fazer NACK
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();

        base.Dispose();
    }
}
