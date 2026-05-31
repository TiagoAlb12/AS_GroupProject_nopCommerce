using OrderIntegrationService.Models;

namespace OrderIntegrationService.Services;

public class MockShippingService
{
    private readonly ILogger<MockShippingService> _logger;

    public MockShippingService(ILogger<MockShippingService> logger)
    {
        _logger = logger;
    }

    public async Task CreateShipmentAsync(OrderCreatedMessage order)
    {
        await Task.Delay(300);

        _logger.LogInformation(
            "Shipping mock: shipment created for order {OrderId}",
            order.OrderId);
    }
}