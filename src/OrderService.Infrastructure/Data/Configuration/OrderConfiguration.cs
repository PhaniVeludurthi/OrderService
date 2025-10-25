using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Core.Entities;

namespace OrderService.Infrastructure.Data.Configuration
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("orders");

            builder.HasKey(o => o.OrderId);

            builder.Property(o => o.OrderId)
                .HasColumnName("order_id")
                .ValueGeneratedOnAdd();  // Auto-increment

            builder.Property(o => o.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            builder.Property(o => o.EventId)
                .HasColumnName("event_id")
                .IsRequired();

            builder.Property(o => o.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(o => o.PaymentStatus)
                .HasColumnName("payment_status")
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(o => o.OrderTotal)
                .HasColumnName("order_total")
                .HasColumnType("decimal(10,2)")
                .IsRequired();

            builder.Property(o => o.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            builder.HasIndex(o => o.UserId)
                .HasDatabaseName("idx_orders_user");

            builder.HasIndex(o => o.EventId)
                .HasDatabaseName("idx_orders_event");

            builder.HasIndex(o => o.Status)
                .HasDatabaseName("idx_orders_status");

            // Relationships
            builder.HasMany(o => o.Tickets)
                .WithOne(t => t.Order)
                .HasForeignKey(t => t.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
