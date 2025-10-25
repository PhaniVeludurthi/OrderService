using OrderService.Core.Entities;

namespace OrderService.Core.Interfaces
{
    public interface ITicketRepository
    {
        Task<Ticket?> GetByIdAsync(int ticketId);
        Task<List<Ticket>> GetByOrderIdAsync(int orderId);
        Task<List<Ticket>> CreateBulkAsync(List<Ticket> tickets);
        Task UpdateAsync(Ticket ticket);
        Task<List<Ticket>> GetByEventIdAsync(int eventId);
        Task<List<Ticket>> GetBySeatIdsAsync(List<int> seatIds);
        Task<Ticket> CreateAsync(Ticket ticket);
    }
}
