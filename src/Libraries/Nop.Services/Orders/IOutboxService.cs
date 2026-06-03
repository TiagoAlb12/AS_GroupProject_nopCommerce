using Nop.Core.Domain.Orders;

namespace Nop.Services.Orders;

public interface IOutboxService
{
    Task EnqueueOrderCreatedEventAsync(Nop.Core.Domain.Orders.OrderCreatedEvent @event);

    Task<IList<OutboxEvent>> GetPendingOrderCreatedEventsAsync(int batchSize = 50);

    Task MarkAsPublishedAsync(OutboxEvent outboxEvent);

    Task MarkAsFailedAsync(OutboxEvent outboxEvent, string errorMessage);
}
