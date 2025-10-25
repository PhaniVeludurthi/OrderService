using Microsoft.Extensions.Logging;
using OrderService.Core.Dtos.Requests;
using OrderService.Core.Dtos.Responses;
using OrderService.Core.Entities;
using OrderService.Core.Interfaces;

namespace OrderService.Infrastructure.Services
{
    public class OrderOrchestrator(
        IOrderRepository orderRepository,
        ITicketRepository ticketRepository,
        ICatalogClient catalogClient,
        ISeatingClient seatingClient,
        IPaymentClient paymentClient,
        ILogger<OrderOrchestrator> logger) : IOrderOrchestrator
    {
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly ITicketRepository _ticketRepository = ticketRepository;
        private readonly ICatalogClient _catalogClient = catalogClient;
        private readonly ISeatingClient _seatingClient = seatingClient;
        private readonly IPaymentClient _paymentClient = paymentClient;
        private readonly ILogger<OrderOrchestrator> _logger = logger;

        public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
        {
            _logger.LogInformation("Starting order creation for User: {UserId}, Event: {EventId}",
                request.UserId, request.EventId);

            // Step 1: Validate event
            var eventDto = await _catalogClient.GetEventAsync(request.EventId);
            if (eventDto == null)
            {
                throw new InvalidOperationException($"Event {request.EventId} not found");
            }

            if (eventDto.Status != "ON_SALE")
            {
                throw new InvalidOperationException($"Event is {eventDto.Status}, not available for booking");
            }

            // Step 2: Get seat details
            var seats = await _catalogClient.GetSeatsAsync(request.SeatIds);
            if (seats == null || seats.Count != request.SeatIds.Count)
            {
                throw new InvalidOperationException("One or more seats not found");
            }

            // Step 3: Reserve seats
            var reservationRequest = new ReserveSeatRequest
            {
                EventId = request.EventId,
                SeatIds = request.SeatIds,
                UserId = request.UserId,
                TtlSeconds = 900 // 15 minutes
            };

            var reservationResult = await _seatingClient.ReserveSeatsAsync(reservationRequest);
            if (!reservationResult.Success)
            {
                throw new InvalidOperationException($"Seat reservation failed: {reservationResult.Message}");
            }

            // Step 4: Calculate total
            decimal subtotal = seats.Sum(s => s.Price);
            decimal tax = Math.Round(subtotal * 0.05m, 2);
            decimal total = subtotal + tax;

            // Step 5: Create order
            var order = new Order
            {
                UserId = request.UserId,
                EventId = request.EventId,
                Status = "CREATED",
                PaymentStatus = "PENDING",
                OrderTotal = total,
                CreatedAt = DateTime.UtcNow
            };

            order = await _orderRepository.CreateAsync(order);

            // Step 6: Process payment
            var paymentRequest = new PaymentRequest
            {
                UserId = order.UserId,
                Amount = order.OrderTotal
            };

            var paymentResult = await _paymentClient.ChargeAsync(paymentRequest);

            if (paymentResult.Success && paymentResult.Status == "SUCCESS")
            {
                // Payment successful - confirm order
                _logger.LogInformation("Payment successful for Order: {OrderId}", order.OrderId);

                // Allocate seats
                await _seatingClient.AllocateSeatsAsync(new AllocateSeatRequest
                {
                    EventId = order.EventId,
                    SeatIds = request.SeatIds
                });

                // Confirm order
                order.Status = "CONFIRMED";
                order.PaymentStatus = "SUCCESS";
                await _orderRepository.UpdateAsync(order);

                // Generate tickets
                var tickets = new List<Ticket>();
                foreach (var seat in seats)
                {
                    tickets.Add(new Ticket
                    {
                        OrderId = order.OrderId,
                        EventId = order.EventId,
                        SeatId = seat.SeatId,
                        PricePaid = seat.Price
                    });
                }

                await _ticketRepository.CreateBulkAsync(tickets);
                order.Tickets = tickets;

                _logger.LogInformation("Order confirmed: {OrderId}", order.OrderId);
            }
            else
            {
                // Payment failed - rollback
                _logger.LogWarning("Payment failed for Order: {OrderId}", order.OrderId);

                await _seatingClient.ReleaseSeatsAsync(new ReleaseSeatRequest
                {
                    EventId = order.EventId,
                    SeatIds = request.SeatIds
                });

                order.Status = "CANCELLED";
                order.PaymentStatus = "FAILED";
                await _orderRepository.UpdateAsync(order);

                throw new InvalidOperationException($"Payment failed: {paymentResult.Message}");
            }

            return MapToOrderResponse(order);
        }

        public async Task<OrderResponse> CancelOrderAsync(int orderId)
        {
            _logger.LogInformation("Cancelling order: {OrderId}", orderId);

            var order = await _orderRepository.GetByIdAsync(orderId) ?? throw new InvalidOperationException($"Order {orderId} not found");
            if (order.Status == "CANCELLED")
            {
                throw new InvalidOperationException("Order already cancelled");
            }

            // Release seats
            var tickets = await _ticketRepository.GetByOrderIdAsync(orderId);
            var seatIds = tickets.Select(t => t.SeatId).ToList();

            await _seatingClient.ReleaseSeatsAsync(new ReleaseSeatRequest
            {
                EventId = order.EventId,
                SeatIds = seatIds
            });

            // Cancel order
            order.Status = "CANCELLED";
            await _orderRepository.UpdateAsync(order);

            return MapToOrderResponse(order);
        }

        private static OrderResponse MapToOrderResponse(Order order)
        {
            return new OrderResponse
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                EventId = order.EventId,
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                OrderTotal = order.OrderTotal,
                CreatedAt = order.CreatedAt,
                Tickets = order.Tickets.Select(t => new TicketResponse
                {
                    TicketId = t.TicketId,
                    OrderId = t.OrderId,
                    EventId = t.EventId,
                    SeatId = t.SeatId,
                    PricePaid = t.PricePaid
                }).ToList()
            };
        }
    }
}
