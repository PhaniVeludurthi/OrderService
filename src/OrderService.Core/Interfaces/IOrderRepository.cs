using OrderService.Core.Entities;

namespace OrderService.Core.Interfaces
{
    public interface IOrderRepository
    {
        Task<Order?> GetByIdAsync(int orderId);
        Task<List<Order>> GetByUserIdAsync(int userId);
        Task<List<Order>> GetByEventIdAsync(int eventId);
        Task<Order> CreateAsync(Order order);
        Task<Order> UpdateAsync(Order order);
        Task<bool> DeleteAsync(int orderId);
        Task<List<Order>> GetAllAsync(int pageNumber = 1, int pageSize = 50);
        Task<int> GetTotalCountAsync();
    }
}
