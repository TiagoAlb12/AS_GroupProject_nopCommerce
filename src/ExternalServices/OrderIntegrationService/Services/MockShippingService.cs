using OrderIntegrationService.Models;

namespace OrderIntegrationService.Services;

public class MockShippingService
{
    private readonly ILogger<MockShippingService> _logger;

    public MockShippingService(ILogger<MockShippingService> logger)
    {
        _logger = logger;
    }

    public async Task<string> CreateShipmentAsync(OrderCreatedMessage order)
    {
        await Task.Delay(300);

        var trackingCode = $"TRK-{order.OrderId}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

        _logger.LogInformation(
            "Shipping mock: shipment created for order {OrderId}, tracking {TrackingCode}",
            order.OrderId,
            trackingCode);

        return trackingCode;
    }
}