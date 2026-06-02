using System;
using System.Text.Json.Serialization;

namespace Nop.Core.Domain.Orders;

/// <summary>
/// Integration event: order created
/// </summary>
public partial class OrderCreatedEvent
{
    /// <summary>
    /// JSON ctor
    /// </summary>
    /// <param name="eventId">Event id</param>
    /// <param name="eventType">Event type</param>
    /// <param name="orderId">Order id</param>
    /// <param name="customerId">Customer id</param>
    /// <param name="createdAt">Created at UTC</param>
    /// <param name="total">Order total</param>
    [JsonConstructor]
    public OrderCreatedEvent(Guid eventId, string eventType, int orderId, int customerId, DateTime createdAt, decimal total)
    {
        EventId = eventId;
        EventType = eventType;
        OrderId = orderId;
        CustomerId = customerId;
        CreatedAt = createdAt;
        Total = total;
    }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="order">The order</param>
    public OrderCreatedEvent(Order order)
    {
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
}
