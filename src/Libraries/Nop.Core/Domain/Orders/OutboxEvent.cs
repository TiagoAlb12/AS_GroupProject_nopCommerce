using Nop.Core;

namespace Nop.Core.Domain.Orders;

/// <summary>
/// Represents an outbox event used for reliable integration publishing
/// </summary>
public partial class OutboxEvent : BaseEntity
{
    public Guid EventId { get; set; }

    public string EventType { get; set; }

    public string Payload { get; set; }

    public int? OrderId { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public DateTime? PublishedOnUtc { get; set; }

    public int Attempts { get; set; }

    public string LastError { get; set; }
}
