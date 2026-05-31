using OrderIntegrationService;
using OrderIntegrationService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<OrderIntegrationStateStore>();
builder.Services.AddSingleton<MockStockService>();
builder.Services.AddSingleton<MockShippingService>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok("Order Integration Service running"));

app.MapGet("/orders/integration-status", (OrderIntegrationStateStore store) =>
{
    return Results.Ok(store.GetAll());
});

app.MapGet("/orders/{orderId:int}/integration-status", (int orderId, OrderIntegrationStateStore store) =>
{
    var status = store.Get(orderId);

    if (status is null)
        return Results.NotFound(new { message = $"No integration status found for order {orderId}" });

    return Results.Ok(status);
});


app.Run();