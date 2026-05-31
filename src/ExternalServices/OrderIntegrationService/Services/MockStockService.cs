using OrderIntegrationService.Models;

namespace OrderIntegrationService.Services;

public class MockStockService
{
    private readonly ILogger<MockStockService> _logger;

    public MockStockService(ILogger<MockStockService> logger)
    {
        _logger = logger;
    }

    public async Task ReserveStockAsync(OrderCreatedMessage order)
    {
        await Task.Delay(300);

        _logger.LogInformation(
            "WMS mock: stock reserved for order {OrderId}, customer {CustomerId}, total {Total}",
            order.OrderId,
            order.CustomerId,
            order.Total);
    }
}