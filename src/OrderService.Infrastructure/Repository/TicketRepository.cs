using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderService.Core.Entities;
using OrderService.Core.Interfaces;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Repository
{
    public class TicketRepository : ITicketRepository
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<TicketRepository> _logger;

        public TicketRepository(OrderDbContext context, ILogger<TicketRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Ticket> GetByIdAsync(int ticketId)
        {
            _logger.LogInformation("Fetching ticket: {TicketId}", ticketId);

            return await _context.Tickets
                .Include(t => t.Order)
                .FirstOrDefaultAsync(t => t.TicketId == ticketId);
        }

        public async Task<List<Ticket>> GetByOrderIdAsync(int orderId)
        {
            _logger.LogInformation("Fetching tickets for order: {OrderId}", orderId);

            return await _context.Tickets
                .Where(t => t.OrderId == orderId)
                .ToListAsync();
        }

        public async Task<List<Ticket>> GetByEventIdAsync(int eventId)
        {
            _logger.LogInformation("Fetching tickets for event: {EventId}", eventId);

            return await _context.Tickets
                .Include(t => t.Order)
                .Where(t => t.EventId == eventId)
                .ToListAsync();
        }

        public async Task<List<Ticket>> GetBySeatIdsAsync(List<string> seatIds)
        {
            _logger.LogInformation("Fetching tickets for {Count} seats", seatIds.Count);

            return await _context.Tickets
                .Where(t => seatIds.Contains(t.SeatId))
                .ToListAsync();
        }

        public async Task<List<Ticket>> CreateBulkAsync(List<Ticket> tickets)
        {
            _logger.LogInformation("Creating {Count} tickets", tickets.Count);

            await _context.Tickets.AddRangeAsync(tickets);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tickets created successfully");

            return tickets;
        }

        public async Task<Ticket> CreateAsync(Ticket ticket)
        {
            _logger.LogInformation("Creating ticket for Order: {OrderId}, Seat: {SeatId}",
                ticket.OrderId, ticket.SeatId);

            await _context.Tickets.AddAsync(ticket);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ticket created with ID: {TicketId}", ticket.TicketId);

            return ticket;
        }

        public async Task UpdateAsync(Ticket ticket)
        {
            _logger.LogInformation("Updating ticket: {TicketId}", ticket.TicketId);

            _context.Tickets.Update(ticket);
            await _context.SaveChangesAsync();
        }
    }
}
