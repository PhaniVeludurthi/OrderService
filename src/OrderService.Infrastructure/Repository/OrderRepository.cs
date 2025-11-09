using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderService.Core.Constants;
using OrderService.Core.Entities;
using OrderService.Core.Interfaces;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Repository
{
    public class OrderRepository(OrderDbContext context, ILogger<OrderRepository> logger) : IOrderRepository
    {
        private readonly OrderDbContext _context = context;
        private readonly ILogger<OrderRepository> _logger = logger;

        public async Task<Order?> GetByIdAsync(int orderId)
        {
            _logger.LogInformation("Fetching order with ID: {OrderId}", orderId);

            return await _context.Orders
                .Include(o => o.Tickets)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
        }

        public async Task<List<Order>> GetByUserIdAsync(int userId)
        {
            _logger.LogInformation("Fetching orders for user: {UserId}", userId);

            return await _context.Orders
                .Include(o => o.Tickets)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Order>> GetByEventIdAsync(int eventId)
        {
            _logger.LogInformation("Fetching orders for event: {EventId}", eventId);

            return await _context.Orders
                .Include(o => o.Tickets)
                .Where(o => o.EventId == eventId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<Order> CreateAsync(Order order)
        {
            _logger.LogInformation("Creating new order for User: {UserId}, Event: {EventId}",
                order.UserId, order.EventId);

            order.CreatedAt = DateTime.UtcNow;

            await _context.Orders.AddAsync(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Order created successfully with ID: {OrderId}", order.OrderId);

            return order;
        }

        public async Task<Order> UpdateAsync(Order order)
        {
            _logger.LogInformation("Updating order: {OrderId}", order.OrderId);

            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Order updated successfully: {OrderId}", order.OrderId);

            return order;
        }

        public async Task<bool> DeleteAsync(int orderId)
        {
            _logger.LogInformation("Deleting order: {OrderId}", orderId);

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderId}", orderId);
                return false;
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Order deleted successfully: {OrderId}", orderId);

            return true;
        }

        public async Task<List<Order>> GetAllAsync(int pageNumber = 1, int pageSize = 50)
        {
            return await _context.Orders
                .Include(o => o.Tickets)
                .OrderByDescending(o => o.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalCountAsync()
        {
            return await _context.Orders.CountAsync();
        }

        public async Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey)
        {
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.Tickets)
                .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey);
        }

        public async Task<List<Order>> GetConfirmedOrdersByEventIdAsync(int eventId)
        {
            return await _context.Orders
                .Where(o => o.EventId == eventId && o.Status == OrderStatus.CONFIRMED)
                .ToListAsync();
        }
    }
}
