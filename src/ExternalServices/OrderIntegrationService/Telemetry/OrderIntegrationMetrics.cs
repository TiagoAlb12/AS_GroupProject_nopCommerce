using System.Diagnostics.Metrics;

namespace OrderIntegrationService.Telemetry;

public static class OrderIntegrationMetrics
{
    public const string MeterName = "OrderIntegrationService.Custom";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> MessagesConsumed =
        Meter.CreateCounter<long>(
            "order_integration_messages_consumed_total",
            description: "Total OrderCreated messages consumed from RabbitMQ");

    public static readonly Counter<long> MessagesProcessed =
        Meter.CreateCounter<long>(
            "order_integration_messages_processed_total",
            description: "Total OrderCreated messages processed successfully");

    public static readonly Counter<long> MessagesProcessingFailed =
        Meter.CreateCounter<long>(
            "order_integration_messages_processing_failed_total",
            description: "Total OrderCreated messages that failed processing");

    public static readonly Counter<long> StockReserved =
        Meter.CreateCounter<long>(
            "order_integration_stock_reserved_total",
            description: "Total stock reservations simulated successfully");

    public static readonly Counter<long> StockReservationFailed =
        Meter.CreateCounter<long>(
            "order_integration_stock_reservation_failed_total",
            description: "Total stock reservation failures");

    public static readonly Counter<long> ShipmentCreated =
        Meter.CreateCounter<long>(
            "order_integration_shipment_created_total",
            description: "Total shipments simulated successfully");

    public static readonly Counter<long> ShipmentCreationFailed =
        Meter.CreateCounter<long>(
            "order_integration_shipment_creation_failed_total",
            description: "Total shipment creation failures");

    public static void RegisterQueueMetrics(QueueMetricsState state)
    {
        Meter.CreateObservableGauge(
            "order_integration_queue_messages_ready",
            () => state.QueueMessagesReady,
            description: "Messages ready in the order.created queue");

        Meter.CreateObservableGauge(
            "order_integration_queue_messages_unacked",
            () => state.QueueMessagesUnacked,
            description: "Messages delivered but not yet acknowledged");

        Meter.CreateObservableGauge(
            "order_integration_dlq_messages_ready",
            () => state.DlqMessagesReady,
            description: "Messages ready in the dead-letter queue");
    }
}