using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Core.Entities;

namespace OrderService.Infrastructure.Data.Configuration
{
    public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
    {
        public void Configure(EntityTypeBuilder<Ticket> builder)
        {
            builder.ToTable("tickets");

            builder.HasKey(t => t.TicketId);

            builder.Property(t => t.TicketId)
                .HasColumnName("ticket_id")
                .ValueGeneratedOnAdd();  // Auto-increment

            builder.Property(t => t.OrderId)
                .HasColumnName("order_id")
                .IsRequired();

            builder.Property(t => t.EventId)
                .HasColumnName("event_id")
                .IsRequired();

            builder.Property(t => t.SeatId)
                .HasColumnName("seat_id")
                .IsRequired();

            builder.Property(t => t.PricePaid)
                .HasColumnName("price_paid")
                .HasColumnType("decimal(10,2)")
                .IsRequired();

            // Indexes
            builder.HasIndex(t => t.OrderId)
                .HasDatabaseName("idx_tickets_order");

            builder.HasIndex(t => t.EventId)
                .HasDatabaseName("idx_tickets_event");

            builder.HasIndex(t => t.SeatId)
                .HasDatabaseName("idx_tickets_seat");
        }
    }
}
