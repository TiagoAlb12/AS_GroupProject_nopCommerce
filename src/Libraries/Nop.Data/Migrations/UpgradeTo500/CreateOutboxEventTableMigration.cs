using FluentMigrator;
using Nop.Core.Domain.Orders;
using Nop.Data.Extensions;

namespace Nop.Data.Migrations.UpgradeTo500;

[NopSchemaMigration("2026-06-02 00:00:00", "Create OutboxEvent table for transactional outbox")] 
public class CreateOutboxEventTableMigration : ForwardOnlyMigration
{
    public override void Up()
    {
        this.CreateTableIfNotExists<OutboxEvent>();
    }
}
