using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Orders;
using Nop.Data.Extensions;

namespace Nop.Data.Mapping.Builders.Orders;

/// <summary>
/// Represents an outbox event entity builder
/// </summary>
public partial class OutboxEventBuilder : NopEntityBuilder<OutboxEvent>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(OutboxEvent.EventId)).AsGuid().NotNullable()
            .WithColumn(nameof(OutboxEvent.EventType)).AsString(int.MaxValue).NotNullable()
            .WithColumn(nameof(OutboxEvent.Payload)).AsString(int.MaxValue).NotNullable()
            .WithColumn(nameof(OutboxEvent.OrderId)).AsInt32().Nullable()
            .WithColumn(nameof(OutboxEvent.CreatedOnUtc)).AsDateTime().NotNullable()
            .WithColumn(nameof(OutboxEvent.PublishedOnUtc)).AsDateTime().Nullable()
            .WithColumn(nameof(OutboxEvent.Attempts)).AsInt32().NotNullable()
            .WithColumn(nameof(OutboxEvent.LastError)).AsString(int.MaxValue).Nullable();
    }
}
