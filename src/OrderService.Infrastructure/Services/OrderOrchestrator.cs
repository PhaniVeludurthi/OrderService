using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OrderService.Core.Constants;
using OrderService.Core.Dtos.Requests;
using OrderService.Core.Dtos.Responses;
using OrderService.Core.Entities;
using OrderService.Core.Events;
using OrderService.Core.Interfaces;
using OrderService.Infrastructure.Metrics;
using System.Diagnostics;

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

                _logger.LogInformation(
                  "Event validated successfully: EventId={EventId}, EventTitle={EventTitle}, EventStatus={EventStatus}, VenueId={VenueId}",
                  eventDto.EventId, eventDto.Title, eventDto.Status, eventDto.VenueId);

                // Validate seats
                var seats = await ValidateSeatsAsync(request.EventId, request.SeatIds);

                _logger.LogInformation(
                   "Seats validated successfully: EventId={EventId}, ValidatedSeatCount={ValidatedSeatCount}, TotalPrice={TotalPrice}",
                   request.EventId, seats.Count, seats.Sum(s => s.Price));

                // Reserve seats
                await ReserveSeatsAsync(request.EventId, request.SeatIds, request.UserId);

                _logger.LogInformation(
                  "Seats reserved successfully: EventId={EventId}, SeatCount={SeatCount}, ReservationTTL={TTL}s",
                  request.EventId, request.SeatIds.Count, SEAT_RESERVATION_TTL_SECONDS);

                // Calculate totals
                var (subtotal, tax, total) = CalculateOrderTotal(seats);
                _logger.LogInformation(
                   "Order totals calculated: EventId={EventId}, Subtotal={Subtotal:C}, Tax={Tax:C} ({TaxRate:P}), Total={Total:C}, SeatCount={SeatCount}",
                   request.EventId, subtotal, tax, TAX_RATE, total, seats.Count);

                // Create order
                var order = await CreateOrderEntityAsync(request, total);

                _logger.LogInformation(
                   "Order entity created: OrderId={OrderId}, UserId={UserId}, EventId={EventId}, Status={Status}, PaymentStatus={PaymentStatus}, Total={Total:C}",
                   order.OrderId, order.UserId, order.EventId, order.Status, order.PaymentStatus, order.OrderTotal);

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

                _logger.LogInformation(
                  "Order retrieved for cancellation: OrderId={OrderId}, CurrentStatus={CurrentStatus}, PaymentStatus={PaymentStatus}, UserId={UserId}, EventId={EventId}",
                  orderId, order.Status, order.PaymentStatus, order.UserId, order.EventId);


                // Check if already cancelled
                if (order.Status == OrderStatus.CANCELLED)
                {
                    _logger.LogWarning("Order already cancelled: OrderId={OrderId}", orderId);
                    throw new InvalidOperationException("Order already cancelled");
                }
                // Check if already refunded
                if (order.Status == OrderStatus.REFUNDED)
                {
                    _logger.LogWarning("Order cancellation skipped - already refunded: OrderId={OrderId}", orderId);
                    throw new InvalidOperationException($"Order {orderId} is already refunded");
                }
                // Get tickets and release seats
                var tickets = await _ticketRepository.GetByOrderIdAsync(orderId);
                _logger.LogInformation(
                  "Retrieved tickets for cancellation: OrderId={OrderId}, TicketCount={TicketCount}",
                  orderId, tickets.Count);
                if (tickets.Count != 0)
                {
                    var seatIds = tickets.Select(t => t.SeatId).ToList();
                    await ReleaseSeatsAsync(order.UserId, order.EventId, seatIds);
                    _logger.LogInformation(
                       "Seats released successfully: OrderId={OrderId}, SeatCount={SeatCount}",
                       orderId, seatIds.Count);
                }

                // If payment was successful, initiate refund
                if (order.PaymentStatus == PaymentStatus.SUCCESS)
                {
                    _logger.LogInformation(
                        "Initiating refund for cancelled order: OrderId={OrderId}, RefundAmount={RefundAmount:C}",
                        orderId, order.OrderTotal);

                    try
                    {
                        var refundRequest = new RefundRequest
                        {
                            OrderId = order.OrderId,
                            Amount = order.OrderTotal,
                            Reason = "Manual order cancellation by user"
                        };

                        var refundResult = await _paymentClient.RefundAsync(refundRequest);

                        if (refundResult.Success)
                        {
                            order.Status = OrderStatus.REFUNDED;
                            order.PaymentStatus = PaymentStatus.REFUNDED;

                            _logger.LogInformation(
                                "Refund processed successfully: OrderId={OrderId}, RefundAmount={RefundAmount:C}",
                                orderId, order.OrderTotal);
                        }
                        else
                        {
                            order.Status = OrderStatus.CANCELLED;

                            _logger.LogWarning(
                                "Refund failed for cancelled order: OrderId={OrderId}, Reason={Reason}",
                                orderId, refundResult.Message);
                        }
                    }
                    catch (Exception refundEx)
                    {
                        order.Status = OrderStatus.CANCELLED;

                        _logger.LogError(refundEx,
                            "Exception during refund processing: OrderId={OrderId} - Manual intervention may be required",
                            orderId);
                    }
                }
                else
                {
                    order.Status = OrderStatus.CANCELLED;

                    _logger.LogInformation(
                        "Order cancelled without refund (payment not completed): OrderId={OrderId}, PaymentStatus={PaymentStatus}",
                        orderId, order.PaymentStatus);
                }

                await _orderRepository.UpdateAsync(order);

                // Publish cancellation/refund event
                if (order.Status == OrderStatus.REFUNDED)
                {
                    await PublishOrderRefundedEventAsync(order, "Manual order cancellation by user", correlationId);
                }
                else
                {
                    await PublishOrderCancelledEventAsync(order, "Manual order cancellation", correlationId);
                }

                _logger.LogInformation(
                    "Order cancellation completed: OrderId={OrderId}, FinalStatus={FinalStatus}, FinalPaymentStatus={FinalPaymentStatus}",
                    orderId,
                    order.Status,
                    order.PaymentStatus);

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
            {
                _logger.LogWarning("Order created without idempotency key - duplicate order risk exists");
                return null;
            }

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
            // Check if event is cancelled
            if (eventDto.Status == EventStatus.CANCELLED)
            {
                _logger.LogWarning(
                    "Event validation failed - event is cancelled: EventId={EventId}, EventTitle={EventTitle}",
                    eventId, eventDto.Title);
                throw new InvalidOperationException($"Event '{eventDto.Title}' (ID: {eventId}) has been cancelled and is not available for booking");
            }

            // Check if event is sold out
            if (eventDto.Status == EventStatus.SOLD_OUT)
            {
                _logger.LogWarning(
                    "Event validation failed - event is sold out: EventId={EventId}, EventTitle={EventTitle}",
                    eventId, eventDto.Title);
                throw new InvalidOperationException($"Event '{eventDto.Title}' (ID: {eventId}) is sold out");
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

            if (seats == null || seats.Count == 0)
            {
                _logger.LogWarning("Seat validation failed - no seats found for event: EventId={EventId}", eventId);
                throw new InvalidOperationException($"No seats found for event {eventId}");
            }

            var foundSeatIds = seats?.Select(s => s.SeatId).ToList() ?? [];
            var missingSeatIds = seatIds.Except(foundSeatIds).ToList();

            if (missingSeatIds.Count > 0)
            {
                _logger.LogWarning(
                    "Seat validation failed - seats not found: EventId={EventId}, MissingSeatIds={MissingSeatIds}, RequestedSeatIds={RequestedSeatIds}",
                    eventId,
                    string.Join(',', missingSeatIds),
                    string.Join(',', seatIds));

                throw new InvalidOperationException(
                    $"One or more seats not found for event {eventId}: {string.Join(", ", missingSeatIds)}");
            }

            var validSeats = seats.Where(x => seatIds.Contains(x.SeatId)).ToList();

            _logger.LogDebug(
                "Seat validation successful: EventId={EventId}, ValidatedSeatCount={ValidatedSeatCount}",
                eventId, validSeats.Count);

            return validSeats;
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

            return order;
        }

        private async Task ProcessPaymentAndCompleteOrderAsync(
            Order order,
            List<string> seatIds,
            List<SeatDto> seats,
            EventDto eventDto,
            string correlationId)
        {
            _logger.LogInformation(
                "Starting payment processing: OrderId={OrderId}, Amount={Amount:C}, UserId={UserId}",
                order.OrderId, order.OrderTotal, order.UserId);
            try
            {
                var paymentRequest = new PaymentRequest
                {
                    OrderId = order.OrderId,
                    UserId = order.UserId,
                    Amount = order.OrderTotal,
                    IdempotencyKey = order.IdempotencyKey ?? Guid.NewGuid().ToString()
                };

                var paymentResult = await _paymentClient.ChargeAsync(paymentRequest);
                _logger.LogInformation(
                   "Payment processing completed: OrderId={OrderId}, Success={Success}, Status={Status}, Message={Message}",
                   order.OrderId, paymentResult.Success, paymentResult.Status, paymentResult.Message);


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
            _logger.LogInformation(
                "Payment successful - proceeding with order fulfillment: OrderId={OrderId}, Amount={Amount:C}",
                order.OrderId, order.OrderTotal);

            try
            {
                // Allocate seats
                await _seatingClient.AllocateSeatsAsync(new AllocateSeatRequest
                {
                    EventId = order.EventId.ToString(),
                    SeatIds = seatIds,
                    UserId = order.UserId.ToString()
                });

                _logger.LogInformation(
                   "Seats allocated successfully: OrderId={OrderId}, SeatCount={SeatCount}",
                   order.OrderId, seatIds.Count);

                // Update order status
                order.Status = OrderStatus.CONFIRMED;
                order.PaymentStatus = PaymentStatus.SUCCESS;
                await _orderRepository.UpdateAsync(order);

                _logger.LogInformation(
                "Order status updated: OrderId={OrderId}, Status={Status}, PaymentStatus={PaymentStatus}",
                order.OrderId, order.Status, order.PaymentStatus);

                // Generate tickets
                var tickets = await GenerateTicketsAsync(order, seats);
                order.Tickets = tickets;

                // Publish event in Outbox
                await PublishOrderConfirmedEventAsync(order, tickets, eventDto, correlationId);

                _logger.LogInformation(
                  "Order fulfillment completed successfully: OrderId={OrderId}, TicketCount={TicketCount}",
                  order.OrderId, tickets.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failure after successful payment! Manual intervention may be needed. OrderId={OrderId}",
                    order.OrderId);
                // CRITICAL: Payment succeeded but fulfillment failed - attempt refund
                try
                {
                    _logger.LogWarning(
                        "Attempting compensating refund: OrderId={OrderId}, Amount={Amount:C}",
                        order.OrderId, order.OrderTotal);

                    var refundRequest = new RefundRequest
                    {
                        OrderId = order.OrderId,
                        Amount = order.OrderTotal,
                        Reason = "Order fulfillment failed after payment - compensating transaction"
                    };

                    var refundResult = await _paymentClient.RefundAsync(refundRequest);

                    if (refundResult.Success)
                    {
                        order.Status = OrderStatus.REFUNDED;
                        order.PaymentStatus = PaymentStatus.REFUNDED;

                        _logger.LogInformation(
                            "Compensating refund successful: OrderId={OrderId}, RefundAmount={RefundAmount:C}",
                            order.OrderId, order.OrderTotal);

                        await PublishOrderRefundedEventAsync(order, "Automatic refund due to fulfillment failure", correlationId);
                    }
                    else
                    {
                        order.Status = OrderStatus.PAYMENT_COMPLETED_BUT_FULFILLMENT_FAILED;

                        _logger.LogError(
                            "Compensating refund FAILED: OrderId={OrderId}, Reason={Reason} - MANUAL INTERVENTION REQUIRED",
                            order.OrderId, refundResult.Message);
                    }
                }
                catch (Exception refundEx)
                {
                    order.Status = OrderStatus.PAYMENT_COMPLETED_BUT_FULFILLMENT_FAILED;

                    _logger.LogError(refundEx,
                        "Exception during compensating refund: OrderId={OrderId} - MANUAL INTERVENTION REQUIRED",
                        order.OrderId);
                }

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

            _logger.LogInformation(
              "Order cancelled due to payment failure: OrderId={OrderId}, FinalStatus={Status}, FinalPaymentStatus={PaymentStatus}",
              order.OrderId, order.Status, order.PaymentStatus);

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
                           "Tickets generated successfully: OrderId={OrderId}, TicketCount={TicketCount}, TotalValue={TotalValue:C}",
                           order.OrderId,
                           tickets.Count,
                           tickets.Sum(t => t.PricePaid));

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
                      "Seats released successfully: EventId={EventId}, SeatCount={SeatCount}, UserId={UserId}",
                      eventId, seatIds.Count, userId);
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

        private async Task PublishOrderRefundedEventAsync(
            Order order,
            string reason,
            string correlationId)
        {
            var orderRefundedEvent = new OrderRefundedEvent
            {
                OrderId = order.OrderId,
                EventId = order.EventId,
                UserId = order.UserId,
                RefundAmount = order.OrderTotal,
                Reason = reason,
                RefundedAt = DateTime.UtcNow,
                CorrelationId = correlationId
            };

            await _outboxRepository.SaveEventAsync(new OutboxEvent
            {
                AggregateType = "Order",
                AggregateId = order.OrderId.ToString(),
                EventType = "OrderRefunded",
                EventPayloadJson = JsonConvert.SerializeObject(orderRefundedEvent),
                CorrelationId = correlationId,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogInformation(
                "OrderRefunded event published to outbox: OrderId={OrderId}, RefundAmount={RefundAmount:C}, Reason={Reason}, EventType={EventType}, CorrelationId={CorrelationId}",
                order.OrderId, order.OrderTotal, reason, "OrderRefunded", correlationId);
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

        public async Task HandleEventCancelledAsync(int eventId)
        {
            var correlationId = _correlationService.GetCorrelationId();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                "Starting event cancellation processing: EventId={EventId}, CorrelationId={CorrelationId}",
                eventId, correlationId);

            try
            {
                // Get all confirmed orders for this event
                var ordersToRefund = await _orderRepository.GetConfirmedOrdersByEventIdAsync(eventId);

                _logger.LogInformation(
                    "Retrieved orders for event cancellation: EventId={EventId}, OrderCount={OrderCount}",
                    eventId, ordersToRefund.Count);

                if (ordersToRefund.Count == 0)
                {
                    _logger.LogInformation(
                        "No orders to refund for cancelled event: EventId={EventId}",
                        eventId);
                    return;
                }

                var successCount = 0;
                var failureCount = 0;
                var totalRefundAmount = 0m;

                foreach (var order in ordersToRefund)
                {
                    try
                    {
                        _logger.LogDebug(
                            "Processing refund for cancelled event: EventId={EventId}, OrderId={OrderId}, RefundAmount={RefundAmount:C}",
                            eventId, order.OrderId, order.OrderTotal);

                        // Request refund from Payment Service
                        var refundRequest = new RefundRequest
                        {
                            OrderId = order.OrderId,
                            Amount = order.OrderTotal,
                            Reason = $"Event {eventId} was cancelled by organizer"
                        };

                        var refundResult = await _paymentClient.RefundAsync(refundRequest);

                        if (refundResult.Success)
                        {
                            order.Status = OrderStatus.REFUNDED;
                            order.PaymentStatus = PaymentStatus.REFUNDED;
                            await _orderRepository.UpdateAsync(order);

                            // Publish refund event
                            await PublishOrderRefundedEventAsync(order, $"Event {eventId} cancelled", correlationId);

                            successCount++;
                            totalRefundAmount += order.OrderTotal;

                            _logger.LogInformation(
                                "Refund successful: EventId={EventId}, OrderId={OrderId}, RefundAmount={RefundAmount:C}",
                                eventId, order.OrderId, order.OrderTotal);
                        }
                        else
                        {
                            failureCount++;

                            _logger.LogError(
                                "Refund failed: EventId={EventId}, OrderId={OrderId}, Reason={Reason}",
                                eventId, order.OrderId, refundResult.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        failureCount++;

                        _logger.LogError(ex,
                            "Exception during refund processing: EventId={EventId}, OrderId={OrderId}",
                            eventId, order.OrderId);
                    }
                }

                stopwatch.Stop();

                _logger.LogInformation(
                    "Event cancellation processing completed: EventId={EventId}, TotalOrders={TotalOrders}, SuccessCount={SuccessCount}, FailureCount={FailureCount}, TotalRefunded={TotalRefunded:C}, ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                    eventId,
                    ordersToRefund.Count,
                    successCount,
                    failureCount,
                    totalRefundAmount,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex,
                    "Event cancellation processing failed: EventId={EventId}, ErrorType={ErrorType}, ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                    eventId,
                    ex.GetType().Name,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
                throw;
            }
        }

        #endregion
    }
}
