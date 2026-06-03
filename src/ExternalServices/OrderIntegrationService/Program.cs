using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OrderIntegrationService;
using OrderIntegrationService.Services;
using OrderIntegrationService.Telemetry;
using Microsoft.EntityFrameworkCore;
using OrderIntegrationService.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("OrderIntegrationDb")
    ?? "Data Source=/app/data/order-integration.db";

builder.Services.AddDbContextFactory<OrderIntegrationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("order-integration-service"));

    options.AddOtlpExporter(otlpOptions =>
    {
        otlpOptions.Endpoint = new Uri("http://telemetry_service:4318/v1/logs");
        otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
    });

    options.AddConsoleExporter();
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(OrderIntegrationMetrics.MeterName)
        .AddOtlpExporter(otlpOptions =>
        {
            otlpOptions.Endpoint = new Uri("http://telemetry_service:4317");
            otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        })
        .AddConsoleExporter());

builder.Services.AddHttpClient();

builder.Services.AddSingleton<OrderIntegrationStateStore>();
builder.Services.AddSingleton<MockStockService>();
builder.Services.AddSingleton<MockShippingService>();
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var queueName = configuration["RabbitMQ:QueueName"] ?? "nopcommerce.order.created";
    var dlqName = configuration["RabbitMQ:DeadLetterQueueName"] ?? "nopcommerce.order.created.dlq";
    return new QueueMetricsState(queueName, dlqName);
});
builder.Services.AddHostedService<RabbitMqQueueMetricsHostedService>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OrderIntegrationDbContext>>();
    using var db = dbFactory.CreateDbContext();
    db.Database.EnsureCreated();
}

OrderIntegrationMetrics.RegisterQueueMetrics(
    app.Services.GetRequiredService<QueueMetricsState>());

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

app.MapGet("/orders/integration-status/view", (OrderIntegrationStateStore store) =>
{
    static string FormatStatus(string status)
    {
        return status switch
        {
            "ShipmentCreated" => "Shipment Created",
            "StockReserved" => "Stock Reserved",
            _ => status
        };
    }

    static string StatusClass(string status)
    {
        return status switch
        {
            "Completed" => "status-completed",
            "Reserved" => "status-success",
            "ShipmentCreated" => "status-success",
            "Failed" => "status-failed",
            "Pending" => "status-pending",
            _ => ""
        };
    }

    var rows = string.Join("", store.GetAll().Select(status => $"""
        <tr>
            <td>#{status.OrderId}</td>
            <td>{status.CustomerId}</td>
            <td>${status.Total:0.00}</td>
            <td><span class="{StatusClass(status.IntegrationStatus)}">{FormatStatus(status.IntegrationStatus)}</span></td>
            <td><span class="{StatusClass(status.StockStatus)}">{FormatStatus(status.StockStatus)}</span></td>
            <td><span class="{StatusClass(status.ShippingStatus)}">{FormatStatus(status.ShippingStatus)}</span></td>
            <td>{status.TrackingCode ?? "-"}</td>
            <td>{status.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC</td>
            <td>{status.LastUpdatedAt:yyyy-MM-dd HH:mm:ss} UTC</td>
        </tr>
    """));

    var html = $$"""
    <!DOCTYPE html>
    <html>
    <head>
        <title>Order Integration Status</title>
        <style>
            body {
                font-family: Arial, sans-serif;
                margin: 40px;
                background: #f7f9fb;
                color: #1f2933;
            }

            .container {
                background: white;
                padding: 28px;
                border-radius: 12px;
                box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08);
            }

            h1 {
                margin-top: 0;
                font-size: 32px;
            }

            p {
                color: #52616b;
                margin-bottom: 24px;
            }

            table {
                border-collapse: collapse;
                width: 100%;
                background: white;
            }

            th, td {
                border: 1px solid #e0e0e0;
                padding: 12px;
                text-align: left;
            }

            th {
                background: #f0f3f5;
                font-weight: 700;
            }

            tr:nth-child(even) {
                background: #fafafa;
            }

            .status-completed,
            .status-success {
                color: #15803d;
                font-weight: 700;
            }

            .status-failed {
                color: #b91c1c;
                font-weight: 700;
            }

            .status-pending {
                color: #b45309;
                font-weight: 700;
            }

            .footer {
                margin-top: 20px;
                font-size: 13px;
                color: #6b7280;
            }
        </style>
    </head>
    <body>
        <div class="container">
            <h1>Order Integration Status</h1>
            <p>External stock and shipping processing visibility after nopCommerce checkout.</p>

            <table>
                <thead>
                    <tr>
                        <th>Order ID</th>
                        <th>Customer ID</th>
                        <th>Order Total</th>
                        <th>Integration Status</th>
                        <th>Stock Status</th>
                        <th>Shipping Status</th>
                        <th>Tracking Code</th>
                        <th>Order Created At</th>
                        <th>Last Updated</th>
                    </tr>
                </thead>
                <tbody>
                    {{rows}}
                </tbody>
            </table>

            <div class="footer">
                This view is provided by the Order Integration Service. WMS and Shipping are simulated external systems.
            </div>
        </div>
    </body>
    </html>
    """;

    return Results.Content(html, "text/html");
});

app.Run();