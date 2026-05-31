namespace OrderIntegrationService.Models;

public class OrderIntegrationStatus
{
    public int OrderId { get; set; }
    public Guid EventId { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }

    public string IntegrationStatus { get; set; } = "Pending";
    public string StockStatus { get; set; } = "Pending";
    public string ShippingStatus { get; set; } = "Pending";

    public string? TrackingCode { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}