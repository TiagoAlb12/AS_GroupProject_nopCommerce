using System.Text.Json;
using Nop.Core.Domain.Orders;
using Nop.Data;

namespace Nop.Services.Orders;

public partial class OutboxService : IOutboxService
{
    protected readonly IRepository<OutboxEvent> _outboxRepository;

    public OutboxService(IRepository<OutboxEvent> outboxRepository)
    {
        _outboxRepository = outboxRepository;
    }

    public virtual async Task EnqueueOrderCreatedEventAsync(OrderCreatedEvent @event)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));

        var outbox = new OutboxEvent
        {
            EventId = @event.EventId == Guid.Empty ? Guid.NewGuid() : @event.EventId,
            EventType = @event.EventType ?? "OrderCreated",
            Payload = JsonSerializer.Serialize(@event),
            OrderId = @event.OrderId,
            CreatedOnUtc = DateTime.UtcNow,
            Attempts = 0
        };

        await _outboxRepository.InsertAsync(outbox);
    }

    public virtual async Task<IList<OutboxEvent>> GetPendingOrderCreatedEventsAsync(int batchSize = 50)
    {
        return await _outboxRepository.GetAllAsync(query => query
            .Where(x => x.EventType == "OrderCreated" && x.PublishedOnUtc == null)
            .OrderBy(x => x.CreatedOnUtc)
            .Take(batchSize));
    }

    public virtual async Task MarkAsPublishedAsync(OutboxEvent outboxEvent)
    {
        if (outboxEvent == null)
            throw new ArgumentNullException(nameof(outboxEvent));

        outboxEvent.PublishedOnUtc = DateTime.UtcNow;
        outboxEvent.LastError = null;

        await _outboxRepository.UpdateAsync(outboxEvent, publishEvent: false);
    }

    public virtual async Task MarkAsFailedAsync(OutboxEvent outboxEvent, string errorMessage)
    {
        if (outboxEvent == null)
            throw new ArgumentNullException(nameof(outboxEvent));

        outboxEvent.Attempts++;
        outboxEvent.LastError = errorMessage ?? string.Empty;

        await _outboxRepository.UpdateAsync(outboxEvent, publishEvent: false);
    }
}
