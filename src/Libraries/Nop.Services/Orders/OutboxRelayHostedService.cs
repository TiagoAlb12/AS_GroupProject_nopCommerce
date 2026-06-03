using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nop.Core.Domain.Orders;

namespace Nop.Services.Orders;

/// <summary>
/// Relays pending outbox events to RabbitMQ.
/// </summary>
public class OutboxRelayHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OutboxRelayHostedService> _logger;

    public OutboxRelayHostedService(IServiceScopeFactory serviceScopeFactory, ILogger<OutboxRelayHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox relay started");

        await ProcessPendingEventsAsync(stoppingToken);

        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessPendingEventsAsync(stoppingToken);
        }
    }

    private async Task ProcessPendingEventsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();

        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
        var publisher = scope.ServiceProvider.GetRequiredService<OrderCreatedRabbitMqPublisher>();

        var pendingEvents = await outboxService.GetPendingOrderCreatedEventsAsync(BatchSize);

        if (!pendingEvents.Any())
        {
            _logger.LogDebug("Outbox relay found no pending events");
            return;
        }

        foreach (var outboxEvent in pendingEvents)
        {
            if (stoppingToken.IsCancellationRequested)
                return;

            try
            {
                var integrationEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(
                    outboxEvent.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (integrationEvent is null)
                    throw new InvalidOperationException($"Failed to deserialize outbox event {outboxEvent.EventId}");

                await publisher.PublishAsync(integrationEvent);
                await outboxService.MarkAsPublishedAsync(outboxEvent);

                _logger.LogInformation(
                    "Published outbox event {EventId} for order {OrderId}",
                    outboxEvent.EventId,
                    outboxEvent.OrderId);
            }
            catch (Exception exception)
            {
                await outboxService.MarkAsFailedAsync(outboxEvent, exception.Message);

                _logger.LogWarning(
                    exception,
                    "Failed to publish outbox event {EventId} for order {OrderId}",
                    outboxEvent.EventId,
                    outboxEvent.OrderId);
            }
        }
    }
}
