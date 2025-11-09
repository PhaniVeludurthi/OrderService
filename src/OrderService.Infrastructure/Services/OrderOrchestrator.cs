using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OrderService.Core.Constants;
using OrderService.Core.Dtos.Requests;
using OrderService.Core.Dtos.Responses;
using OrderService.Core.Entities;
using OrderService.Core.Events;
using OrderService.Core.Interfaces;
using OrderService.Infrastructure.Metrics;

namespace OrderService.Infrastructure.Services
{
    public class OrderOrchestrator(
        IOrderRepository orderRepository,
        ITicketRepository ticketRepository,
        ICatalogClient catalogClient,
        ISeatingClient seatingClient,
        IPaymentClient paymentClient,
        ICorrelationService correlationService,
        IOutboxRepository outboxRepository,
        ILogger<OrderOrchestrator> logger) : IOrderOrchestrator
    {
        private readonly IOrderRepository _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        private readonly ITicketRepository _ticketRepository = ticketRepository ?? throw new ArgumentNullException(nameof(ticketRepository));
        private readonly ICatalogClient _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
        private readonly ISeatingClient _seatingClient = seatingClient ?? throw new ArgumentNullException(nameof(seatingClient));
        private readonly IPaymentClient _paymentClient = paymentClient ?? throw new ArgumentNullException(nameof(paymentClient));
        private readonly ICorrelationService _correlationService = correlationService ?? throw new ArgumentNullException(nameof(correlationService));
        private readonly IOutboxRepository _outboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(outboxRepository));
        private readonly ILogger<OrderOrchestrator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private const int SEAT_RESERVATION_TTL_SECONDS = 900; // 15 minutes
        private const decimal TAX_RATE = 0.05m;

        public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var correlationId = _correlationService.GetCorrelationId();

            _logger.LogInformation(
                "Starting order creation: EventId={EventId}, Seats={Seats}, UserId={UserId}, CorrelationId={CorrelationId}",
                request.EventId,
                string.Join(',', request.SeatIds),
                request.UserId,
                correlationId);

            try
            {
                // Check for idempotency
                var existingOrder = await CheckIdempotencyAsync(request.IdempotencyKey);
                if (existingOrder != null)
                {
                    _logger.LogInformation(
                        "Returning existing order for idempotency key: {IdempotencyKey}, OrderId={OrderId}",
                        request.IdempotencyKey,
                        existingOrder.OrderId);
                    return MapToOrderResponse(existingOrder);
                }

                // Validate event
                var eventDto = await ValidateEventAsync(request.EventId);

                // Validate seats
                var seats = await ValidateSeatsAsync(request.EventId, request.SeatIds);

                // Reserve seats
                await ReserveSeatsAsync(request.EventId, request.SeatIds, request.UserId);

                // Calculate totals
                var (subtotal, tax, total) = CalculateOrderTotal(seats);

                // Create order
                var order = await CreateOrderEntityAsync(request, total);

                // Process payment and complete order
                await ProcessPaymentAndCompleteOrderAsync(order, request.SeatIds, seats, eventDto, correlationId);

                _logger.LogInformation(
                    "Order creation completed: OrderId={OrderId}, Status={Status}, CorrelationId={CorrelationId}",
                    order.OrderId,
                    order.Status,
                    correlationId);

                BusinessMetrics.OrdersTotal.Inc();

                return MapToOrderResponse(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Order creation failed: EventId={EventId}, UserId={UserId}, CorrelationId={CorrelationId}",
                    request.EventId,
                    request.UserId,
                    correlationId);
                throw;
            }
        }

        public async Task<OrderResponse> CancelOrderAsync(int orderId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orderId);

            var correlationId = _correlationService.GetCorrelationId();

            _logger.LogInformation(
                "Starting order cancellation: OrderId={OrderId}, CorrelationId={CorrelationId}",
                orderId,
                correlationId);

            try
            {
                // Get order
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null)
                {
                    _logger.LogWarning("Order not found: OrderId={OrderId}", orderId);
                    throw new InvalidOperationException($"Order {orderId} not found");
                }

                // Check if already cancelled
                if (order.Status == OrderStatus.CANCELLED)
                {
                    _logger.LogWarning("Order already cancelled: OrderId={OrderId}", orderId);
                    throw new InvalidOperationException("Order already cancelled");
                }

                // Get tickets and release seats
                var tickets = await _ticketRepository.GetByOrderIdAsync(orderId);
                if (tickets.Count != 0)
                {
                    var seatIds = tickets.Select(t => t.SeatId).ToList();
                    await ReleaseSeatsAsync(order.UserId, order.EventId, seatIds);
                }

                // Update order status
                order.Status = OrderStatus.CANCELLED;
                await _orderRepository.UpdateAsync(order);

                // Publish cancellation event
                await PublishOrderCancelledEventAsync(order, "Manual cancellation", correlationId);

                _logger.LogInformation(
                    "Order cancelled successfully: OrderId={OrderId}, CorrelationId={CorrelationId}",
                    orderId,
                    correlationId);

                return MapToOrderResponse(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Order cancellation failed: OrderId={OrderId}, CorrelationId={CorrelationId}",
                    orderId,
                    correlationId);
                throw;
            }
        }

        #region Private Helper Methods

        private async Task<Order?> CheckIdempotencyAsync(string? idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return null;

            return await _orderRepository.GetByIdempotencyKeyAsync(idempotencyKey);
        }

        private async Task<EventDto> ValidateEventAsync(int eventId)
        {
            var eventDto = await _catalogClient.GetEventAsync(eventId);

            if (eventDto == null)
            {
                _logger.LogWarning("Event not found: EventId={EventId}", eventId);
                throw new InvalidOperationException($"Event {eventId} not found");
            }

            if (eventDto.Status != EventStatus.ONSALE)
            {
                _logger.LogWarning(
                    "Event not available for sale: EventId={EventId}, Status={Status}",
                    eventId,
                    eventDto.Status);
                throw new InvalidOperationException($"Event {eventId} is not available for sale");
            }

            return eventDto;
        }

        private async Task<List<SeatDto>> ValidateSeatsAsync(int eventId, List<string> seatIds)
        {
            if (seatIds == null || seatIds.Count == 0)
            {
                throw new InvalidOperationException("At least one seat must be selected");
            }

            var seats = await _seatingClient.GetSeatsAsync(eventId);
            var foundSeatIds = seats?.Select(s => s.SeatId).ToList() ?? [];
            var missingSeatIds = seatIds.Except(foundSeatIds).ToList();

            if (missingSeatIds.Count > 0)
            {
                _logger.LogWarning(
                    "Seats not found: MissingSeatIds={MissingSeatIds}",
                    string.Join(',', missingSeatIds));

                throw new InvalidOperationException(
                    $"One or more seats not found: {string.Join(',', missingSeatIds)}");
            }

            return seats;
        }

        private async Task ReserveSeatsAsync(int eventId, List<string> seatIds, int userId)
        {
            var reservationRequest = new ReserveSeatRequest
            {
                EventId = eventId.ToString(),
                SeatIds = seatIds,
                UserId = userId.ToString(),
                TtlSeconds = SEAT_RESERVATION_TTL_SECONDS
            };

            var reservationResult = await _seatingClient.ReserveSeatsAsync(reservationRequest);

            if (!reservationResult.Success)
            {
                _logger.LogWarning(
                    "Seat reservation failed: EventId={EventId}, SeatIds={SeatIds}, Reason={Reason}",
                    eventId,
                    string.Join(',', seatIds),
                    reservationResult.Message);

                BusinessMetrics.SeatReservationsFailed.Inc();

                throw new InvalidOperationException(
                    $"Seat reservation failed: {reservationResult.Message}");
            }

            _logger.LogInformation(
                "Seats reserved successfully: EventId={EventId}, SeatIds={SeatIds}",
                eventId,
                string.Join(',', seatIds));
        }

        private static (decimal subtotal, decimal tax, decimal total) CalculateOrderTotal(List<SeatDto> seats)
        {
            var subtotal = seats.Sum(s => s.Price);
            var tax = Math.Round(subtotal * TAX_RATE, 2);
            var total = subtotal + tax;

            return (subtotal, tax, total);
        }

        private async Task<Order> CreateOrderEntityAsync(CreateOrderRequest request, decimal total)
        {
            var order = new Order
            {
                UserId = request.UserId,
                EventId = request.EventId,
                Status = OrderStatus.CREATED,
                PaymentStatus = PaymentStatus.PENDING,
                OrderTotal = total,
                CreatedAt = DateTime.UtcNow,
                IdempotencyKey = request.IdempotencyKey,
            };

            order = await _orderRepository.CreateAsync(order);

            _logger.LogInformation(
                "Order entity created: OrderId={OrderId}, Total={Total}",
                order.OrderId,
                order.OrderTotal);

            return order;
        }

        private async Task ProcessPaymentAndCompleteOrderAsync(
            Order order,
            List<string> seatIds,
            List<SeatDto> seats,
            EventDto eventDto,
            string correlationId)
        {
            try
            {
                var paymentRequest = new PaymentRequest
                {
                    UserId = order.UserId,
                    Amount = order.OrderTotal
                };

                var paymentResult = await _paymentClient.ChargeAsync(paymentRequest);

                if (paymentResult.Success && paymentResult.Status == PaymentStatus.SUCCESS)
                {
                    await HandleSuccessfulPaymentAsync(order, seatIds, seats, eventDto, correlationId);
                }
                else
                {
                    await HandleFailedPaymentAsync(order, seatIds, paymentResult.Message ?? "Unknown error", correlationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Payment or order completion crashed. OrderId={OrderId}, CorrelationId={CorrelationId}",
                    order.OrderId, correlationId);

                // Compensation - Release seats & mark order as failed
                await HandleFailedPaymentAsync(order, seatIds, "Unexpected error during payment", correlationId);
                throw; // Bubble up
            }
        }

        private async Task HandleSuccessfulPaymentAsync(
            Order order,
            List<string> seatIds,
            List<SeatDto> seats,
            EventDto eventDto,
            string correlationId)
        {
            _logger.LogInformation("Payment successful for OrderId={OrderId}", order.OrderId);

            try
            {
                // Allocate seats
                await _seatingClient.AllocateSeatsAsync(new AllocateSeatRequest
                {
                    EventId = order.EventId.ToString(),
                    SeatIds = seatIds,
                    UserId = order.UserId.ToString()
                });

                // Update order status
                order.Status = OrderStatus.CONFIRMED;
                order.PaymentStatus = PaymentStatus.SUCCESS;
                await _orderRepository.UpdateAsync(order);

                // Generate tickets
                var tickets = await GenerateTicketsAsync(order, seats);
                order.Tickets = tickets;

                // Publish event in Outbox
                await PublishOrderConfirmedEventAsync(order, tickets, eventDto, correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failure after successful payment! Manual intervention may be needed. OrderId={OrderId}",
                    order.OrderId);

                // Mark as failed but DONT refund here automatically
                order.Status = OrderStatus.PAYMENT_COMPLETED_BUT_FULFILLMENT_FAILED;
                await _orderRepository.UpdateAsync(order);
            }
        }

        private async Task HandleFailedPaymentAsync(
            Order order,
            List<string> seatIds,
            string failureReason,
            string correlationId)
        {
            _logger.LogWarning(
                "Payment failed for OrderId={OrderId}, Reason={Reason}",
                order.OrderId,
                failureReason);

            // Release seats
            await ReleaseSeatsAsync(order.UserId, order.EventId, seatIds);

            // Update order status
            order.Status = OrderStatus.CANCELLED;
            order.PaymentStatus = PaymentStatus.FAILED;
            await _orderRepository.UpdateAsync(order);

            BusinessMetrics.PaymentsFailedTotal.Inc();

            // Publish cancellation event
            await PublishOrderCancelledEventAsync(order, failureReason, correlationId);
        }

        private async Task<List<Ticket>> GenerateTicketsAsync(Order order, List<SeatDto> seats)
        {
            var tickets = seats.Select(seat => new Ticket
            {
                OrderId = order.OrderId,
                EventId = order.EventId,
                SeatId = seat.SeatId,
                PricePaid = seat.Price
            }).ToList();

            await _ticketRepository.CreateBulkAsync(tickets);

            _logger.LogInformation(
                "Tickets generated: OrderId={OrderId}, TicketCount={Count}",
                order.OrderId,
                tickets.Count);

            return tickets;
        }

        private async Task ReleaseSeatsAsync(int userId, int eventId, List<string> seatIds)
        {
            try
            {
                await _seatingClient.ReleaseSeatsAsync(new ReleaseSeatRequest
                {
                    EventId = eventId.ToString(),
                    SeatIds = seatIds,
                    UserId = userId.ToString()
                });

                _logger.LogInformation(
                    "Seats released: EventId={EventId}, SeatIds={SeatIds}",
                    eventId,
                    string.Join(',', seatIds));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to release seats: EventId={EventId}, SeatIds={SeatIds}",
                    eventId,
                    string.Join(',', seatIds));
                // Don't throw - log and continue
            }
        }

        private async Task PublishOrderConfirmedEventAsync(
            Order order,
            List<Ticket> tickets,
            EventDto eventDto,
            string correlationId)
        {
            var orderConfirmedEvent = new OrderConfirmedEvent
            {
                OrderId = order.OrderId,
                EventId = order.EventId,
                UserId = order.UserId,
                EventTitle = eventDto.Title,
                OrderTotal = order.OrderTotal,
                SeatIds = tickets.Select(t => t.SeatId).ToList(),
                ConfirmedAt = DateTime.UtcNow,
                CorrelationId = correlationId
            };

            await _outboxRepository.SaveEventAsync(new OutboxEvent
            {
                AggregateType = "Order",
                AggregateId = order.OrderId.ToString(),
                EventType = "OrderConfirmed",
                EventPayloadJson = JsonConvert.SerializeObject(orderConfirmedEvent),
                CorrelationId = correlationId,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogInformation(
                "OrderConfirmed event published: OrderId={OrderId}, CorrelationId={CorrelationId}",
                order.OrderId,
                correlationId);
        }

        private async Task PublishOrderCancelledEventAsync(
            Order order,
            string reason,
            string correlationId)
        {
            var orderCancelledEvent = new OrderCancelledEvent
            {
                OrderId = order.OrderId,
                Reason = reason,
                CancelledAt = DateTime.UtcNow,
                CorrelationId = correlationId
            };

            await _outboxRepository.SaveEventAsync(new OutboxEvent
            {
                AggregateType = "Order",
                AggregateId = order.OrderId.ToString(),
                EventType = "OrderCancelled",
                EventPayloadJson = JsonConvert.SerializeObject(orderCancelledEvent),
                CorrelationId = correlationId,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogInformation(
                "OrderCancelled event published: OrderId={OrderId}, Reason={Reason}, CorrelationId={CorrelationId}",
                order.OrderId,
                reason,
                correlationId);
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
                Tickets = order.Tickets?.Select(t => new TicketResponse
                {
                    TicketId = t.TicketId,
                    OrderId = t.OrderId,
                    EventId = t.EventId,
                    SeatId = t.SeatId,
                    PricePaid = t.PricePaid
                }).ToList() ?? new List<TicketResponse>()
            };
        }

        #endregion
    }
}
