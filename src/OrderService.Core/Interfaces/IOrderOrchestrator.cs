using OrderService.Core.Dtos.Requests;
using OrderService.Core.Dtos.Responses;

namespace OrderService.Core.Interfaces
{
    public interface IOrderOrchestrator
    {
        Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request);
        Task<OrderResponse> CancelOrderAsync(int orderId);
        Task HandleEventCancelledAsync(int eventId);
    }
}
