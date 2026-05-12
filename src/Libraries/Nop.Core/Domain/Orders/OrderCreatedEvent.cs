using System;

namespace Nop.Core.Domain.Orders;

/// <summary>
/// Integration event: order created
/// </summary>
public partial class OrderCreatedEvent
{
    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="order">The order</param>
    public OrderCreatedEvent(Order order)
    {
        Order = order;
        EventId = Guid.NewGuid();
        EventType = "OrderCreated";
        OrderId = order.Id;
        CustomerId = order.CustomerId;
        CreatedAt = order.CreatedOnUtc;
        Total = order.OrderTotal;
    }

    /// <summary>
    /// Event id
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// Event type
    /// </summary>
    public string EventType { get; }

    /// <summary>
    /// Order id
    /// </summary>
    public int OrderId { get; }

    /// <summary>
    /// Customer id
    /// </summary>
    public int CustomerId { get; }

    /// <summary>
    /// Created at UTC
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Order total
    /// </summary>
    public decimal Total { get; }

    /// <summary>
    /// Original order object (optional)
    /// </summary>
    public Order Order { get; }
}
