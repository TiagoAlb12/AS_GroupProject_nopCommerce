using System.Collections.Concurrent;
using OrderIntegrationService.Models;

namespace OrderIntegrationService.Services;

public class OrderIntegrationStateStore
{
    private readonly ConcurrentDictionary<int, OrderIntegrationStatus> _statuses = new();

    public IEnumerable<OrderIntegrationStatus> GetAll()
    {
        return _statuses.Values.OrderByDescending(s => s.LastUpdatedAt);
    }

    public OrderIntegrationStatus? Get(int orderId)
    {
        _statuses.TryGetValue(orderId, out var status);
        return status;
    }

    public void SetReceived(OrderCreatedMessage order)
    {
        _statuses[order.OrderId] = new OrderIntegrationStatus
        {
            OrderId = order.OrderId,
            EventId = order.EventId,
            CustomerId = order.CustomerId,
            Total = order.Total,
            CreatedAt = order.CreatedAt,
            IntegrationStatus = "Received",
            StockStatus = "Pending",
            ShippingStatus = "Pending",
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    public void SetStockReserved(int orderId)
    {
        Update(orderId, status =>
        {
            status.StockStatus = "Reserved";
            status.IntegrationStatus = "StockReserved";
        });
    }

    public void SetShipmentCreated(int orderId, string trackingCode)
    {
        Update(orderId, status =>
        {
            status.ShippingStatus = "ShipmentCreated";
            status.TrackingCode = trackingCode;
            status.IntegrationStatus = "ShipmentCreated";
        });
    }

    public void SetCompleted(int orderId)
    {
        Update(orderId, status =>
        {
            status.IntegrationStatus = "Completed";
            status.ErrorMessage = null;
        });
    }

    public void SetFailed(int orderId, string errorMessage)
    {
        Update(orderId, status =>
        {
            status.IntegrationStatus = "Failed";
            status.ErrorMessage = errorMessage;
        });
    }

    private void Update(int orderId, Action<OrderIntegrationStatus> update)
    {
        if (!_statuses.TryGetValue(orderId, out var status))
            return;

        update(status);
        status.LastUpdatedAt = DateTime.UtcNow;
    }
}