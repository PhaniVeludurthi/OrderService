using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderService.Core.Entities;
using System.Globalization;

namespace OrderService.Infrastructure.Data
{
    public class DatabaseSeeder(OrderDbContext context, ILogger<DatabaseSeeder> logger)
    {
        private readonly OrderDbContext _context = context;
        private readonly ILogger<DatabaseSeeder> _logger = logger;

        public async Task SeedAsync()
        {
            _logger.LogInformation("Starting database seeding...");

            try
            {
                // Ensure database is created and migrations applied
                await _context.Database.MigrateAsync();
                _logger.LogInformation("Database migrations applied");

                // Seed orders if table is empty
                var orderCount = await _context.Orders.CountAsync();
                if (orderCount == 0)
                {
                    await SeedOrdersAsync();
                }
                else
                {
                    _logger.LogInformation("Orders table already has {Count} records, skipping seed", orderCount);
                }

                // Seed tickets if table is empty
                var ticketCount = await _context.Tickets.CountAsync();
                if (ticketCount == 0)
                {
                    await SeedTicketsAsync();
                }
                else
                {
                    _logger.LogInformation("Tickets table already has {Count} records, skipping seed", ticketCount);
                }

                _logger.LogInformation("Database seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while seeding database");
                throw;
            }
        }

        private async Task SeedOrdersAsync()
        {
            var csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SeedData", "etsr_orders.csv");

            if (!File.Exists(csvPath))
            {
                _logger.LogWarning("Orders CSV file not found at: {Path}", csvPath);
                return;
            }

            _logger.LogInformation("Seeding orders from CSV: {Path}", csvPath);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null
            };

            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, config);

            csv.Context.RegisterClassMap<OrderCsvMap>();
            var records = csv.GetRecords<OrderCsv>().ToList();

            _logger.LogInformation("Found {Count} orders in CSV", records.Count);

            var orders = records.Select(r => new Order
            {
                OrderId = r.OrderId,
                UserId = r.UserId,
                EventId = r.EventId,
                Status = r.Status,
                PaymentStatus = r.PaymentStatus,
                OrderTotal = r.OrderTotal,
                CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)
            }).ToList();

            // Add in batches to avoid memory issues
            const int batchSize = 100;
            for (int i = 0; i < orders.Count; i += batchSize)
            {
                var batch = orders.Skip(i).Take(batchSize).ToList();
                await _context.Orders.AddRangeAsync(batch);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Seeded batch {Batch}/{Total} orders",
                    Math.Min(i + batchSize, orders.Count), orders.Count);
            }

            // Reset identity sequence
            await ResetOrderSequenceAsync();

            _logger.LogInformation("Successfully seeded {Count} orders", orders.Count);
        }

        private async Task SeedTicketsAsync()
        {
            var csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SeedData", "etsr_tickets.csv");

            if (!File.Exists(csvPath))
            {
                _logger.LogWarning("Tickets CSV file not found at: {Path}", csvPath);
                return;
            }

            _logger.LogInformation("Seeding tickets from CSV: {Path}", csvPath);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null
            };

            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, config);

            csv.Context.RegisterClassMap<TicketCsvMap>();
            var records = csv.GetRecords<TicketCsv>().ToList();

            _logger.LogInformation("Found {Count} tickets in CSV", records.Count);

            var tickets = records.Select(r => new Ticket
            {
                TicketId = r.TicketId,
                OrderId = r.OrderId,
                EventId = r.EventId,
                SeatId = r.SeatId.ToString(),
                PricePaid = r.PricePaid
            }).ToList();

            // Add in batches
            const int batchSize = 100;
            for (int i = 0; i < tickets.Count; i += batchSize)
            {
                var batch = tickets.Skip(i).Take(batchSize).ToList();
                await _context.Tickets.AddRangeAsync(batch);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Seeded batch {Batch}/{Total} tickets",
                    Math.Min(i + batchSize, tickets.Count), tickets.Count);
            }

            // Reset identity sequence
            await ResetTicketSequenceAsync();

            _logger.LogInformation("Successfully seeded {Count} tickets", tickets.Count);
        }

        private async Task ResetOrderSequenceAsync()
        {
            try
            {
                var maxId = await _context.Orders.MaxAsync(o => (int?)o.OrderId) ?? 0;
                await _context.Database.ExecuteSqlAsync(
                    $"SELECT setval('orders_order_id_seq', {maxId}, true)");

                _logger.LogInformation("Reset orders sequence to {MaxId}", maxId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not reset orders sequence");
            }
        }

        private async Task ResetTicketSequenceAsync()
        {
            try
            {
                var maxId = await _context.Tickets.MaxAsync(t => (int?)t.TicketId) ?? 0;
                await _context.Database.ExecuteSqlAsync(
                    $"SELECT setval('tickets_ticket_id_seq', {maxId}, true)");

                _logger.LogInformation("Reset tickets sequence to {MaxId}", maxId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not reset tickets sequence");
            }
        }
    }

    // CSV mapping classes
    public class OrderCsv
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public int EventId { get; set; }
        public string Status { get; set; }
        public string PaymentStatus { get; set; }
        public decimal OrderTotal { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TicketCsv
    {
        public int TicketId { get; set; }
        public int OrderId { get; set; }
        public int EventId { get; set; }
        public int SeatId { get; set; }
        public decimal PricePaid { get; set; }
    }

    // CSV class maps
    public sealed class OrderCsvMap : ClassMap<OrderCsv>
    {
        public OrderCsvMap()
        {
            Map(m => m.OrderId).Name("order_id");
            Map(m => m.UserId).Name("user_id");
            Map(m => m.EventId).Name("event_id");
            Map(m => m.Status).Name("status");
            Map(m => m.PaymentStatus).Name("payment_status");
            Map(m => m.OrderTotal).Name("order_total");
            Map(m => m.CreatedAt).Name("created_at")
                            .TypeConverterOption.Format("yyyy-MM-dd HH:mm:ss");
        }
    }

    public sealed class TicketCsvMap : ClassMap<TicketCsv>
    {
        public TicketCsvMap()
        {
            Map(m => m.TicketId).Name("ticket_id");
            Map(m => m.OrderId).Name("order_id");
            Map(m => m.EventId).Name("event_id");
            Map(m => m.SeatId).Name("seat_id");
            Map(m => m.PricePaid).Name("price_paid");
        }
    }
}
