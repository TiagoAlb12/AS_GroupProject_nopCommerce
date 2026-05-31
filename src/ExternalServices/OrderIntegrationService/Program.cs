using OrderIntegrationService;
using OrderIntegrationService.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();

builder.Services.AddSingleton<MockStockService>();
builder.Services.AddSingleton<MockShippingService>();

var host = builder.Build();
host.Run();