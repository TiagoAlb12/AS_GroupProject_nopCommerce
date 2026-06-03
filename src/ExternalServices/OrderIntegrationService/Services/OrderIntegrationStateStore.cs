using Microsoft.EntityFrameworkCore;
using OrderIntegrationService.Data;
using OrderIntegrationService.Models;

namespace OrderIntegrationService.Services;

public class OrderIntegrationStateStore
{
    private readonly IDbContextFactory<OrderIntegrationDbContext> _dbContextFactory;

    public OrderIntegrationStateStore(IDbContextFactory<OrderIntegrationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public IEnumerable<OrderIntegrationStatus> GetAll()
    {
        using var db = _dbContextFactory.CreateDbContext();

        return db.OrderIntegrationStatuses
            .AsNoTracking()
            .OrderByDescending(s => s.LastUpdatedAt)
            .ToList();
    }

    public OrderIntegrationStatus? Get(int orderId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        return db.OrderIntegrationStatuses
            .AsNoTracking()
            .FirstOrDefault(s => s.OrderId == orderId);
    }

    public void SetReceived(OrderCreatedMessage order)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var status = db.OrderIntegrationStatuses
            .FirstOrDefault(s => s.OrderId == order.OrderId);

        if (status is null)
        {
            status = new OrderIntegrationStatus
            {
                OrderId = order.OrderId
            };

            db.OrderIntegrationStatuses.Add(status);
        }

        status.EventId = order.EventId;
        status.CustomerId = order.CustomerId;
        status.Total = order.Total;
        status.CreatedAt = order.CreatedAt;
        status.IntegrationStatus = "Received";
        status.StockStatus = "Pending";
        status.ShippingStatus = "Pending";
        status.TrackingCode = null;
        status.ErrorMessage = null;
        status.LastUpdatedAt = DateTime.UtcNow;

        db.SaveChanges();
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
        using var db = _dbContextFactory.CreateDbContext();

        var status = db.OrderIntegrationStatuses
            .FirstOrDefault(s => s.OrderId == orderId);

        if (status is null)
            return;

        update(status);
        status.LastUpdatedAt = DateTime.UtcNow;

        db.SaveChanges();
    }
}