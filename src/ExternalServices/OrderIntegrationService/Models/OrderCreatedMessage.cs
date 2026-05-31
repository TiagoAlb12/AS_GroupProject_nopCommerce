namespace OrderIntegrationService.Models;

public class OrderCreatedMessage
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Total { get; set; }
}