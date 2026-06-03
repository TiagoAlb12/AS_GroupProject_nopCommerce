using Microsoft.EntityFrameworkCore;
using OrderIntegrationService.Models;

namespace OrderIntegrationService.Data;

public class OrderIntegrationDbContext : DbContext
{
    public OrderIntegrationDbContext(DbContextOptions<OrderIntegrationDbContext> options)
        : base(options)
    {
    }

    public DbSet<OrderIntegrationStatus> OrderIntegrationStatuses => Set<OrderIntegrationStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderIntegrationStatus>()
            .HasKey(s => s.OrderId);
    }
}
