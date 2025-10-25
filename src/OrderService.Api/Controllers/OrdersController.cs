using Microsoft.AspNetCore.Mvc;
using OrderService.Core.Dtos.Requests;
using OrderService.Core.Dtos.Responses;
using OrderService.Core.Interfaces;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;

namespace OrderService.Api.Controllers
{
    [ApiController]
    [Route("api/v1/orders")]
    [Produces("application/json")]
    [SwaggerTag("Order management endpoints for creating, viewing, and cancelling orders")]
    public class OrdersController(
        IOrderRepository orderRepository,
        IOrderOrchestrator orderOrchestrator,
        ILogger<OrdersController> logger) : ControllerBase
    {
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly IOrderOrchestrator _orderOrchestrator = orderOrchestrator;
        private readonly ILogger<OrdersController> _logger = logger;

        /// <summary>
        /// Get all orders with pagination
        /// </summary>
        /// <param name="page">Page number (starts from 1)</param>
        /// <param name="pageSize">Number of items per page (max 100)</param>
        /// <returns>Paginated list of orders</returns>
        /// <response code="200">Returns the paginated list of orders</response>
        [HttpGet]
        [SwaggerOperation(
            Summary = "Get all orders",
            Description = "Retrieves a paginated list of all orders in the system",
            OperationId = "GetAllOrders",
            Tags = new[] { "Orders" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(object))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllOrders(
            [FromQuery, SwaggerParameter("Page number (default: 1)")] int page = 1,
            [FromQuery, SwaggerParameter("Page size (default: 50, max: 100)")] int pageSize = 50)
        {
            _logger.LogInformation("Fetching orders - Page: {Page}, PageSize: {PageSize}", page, pageSize);

            // Validate pagination
            page = Math.Max(1, page);
            pageSize = Math.Min(100, Math.Max(1, pageSize));

            var orders = await _orderRepository.GetAllAsync(page, pageSize);
            var totalCount = await _orderRepository.GetTotalCountAsync();

            var orderDtos = orders.Select(MapToOrderDto).ToList();

            var response = new
            {
                data = orderDtos,
                pagination = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    totalCount = totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                }
            };

            return Ok(response);
        }

        /// <summary>
        /// Get order by ID
        /// </summary>
        /// <param name="id">Order ID</param>
        /// <returns>Order details with tickets</returns>
        /// <response code="200">Returns the order details</response>
        /// <response code="404">Order not found</response>
        [HttpGet("{id:int}")]
        [SwaggerOperation(
            Summary = "Get order by ID",
            Description = "Retrieves detailed information about a specific order including all associated tickets",
            OperationId = "GetOrderById",
            Tags = new[] { "Orders" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(OrderResponse))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "Order not found", typeof(ErrorResponse))]
        [SwaggerResponseExample(StatusCodes.Status200OK, typeof(Examples.OrderResponseExample))]
        [SwaggerResponseExample(StatusCodes.Status404NotFound, typeof(Examples.ErrorResponseExample))]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrder(
            [FromRoute, SwaggerParameter("Unique order identifier", Required = true)] int id)
        {
            _logger.LogInformation("Fetching order: {OrderId}", id);

            var order = await _orderRepository.GetByIdAsync(id);
            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderId}", id);
                return NotFound(new ErrorResponse($"Order {id} not found"));
            }

            var orderDto = MapToOrderDto(order);
            return Ok(orderDto);
        }

        /// <summary>
        /// Get all orders for a specific user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of orders for the user</returns>
        /// <response code="200">Returns user's orders</response>
        [HttpGet("user/{userId:int}")]
        [SwaggerOperation(
            Summary = "Get user orders",
            Description = "Retrieves all orders placed by a specific user",
            OperationId = "GetUserOrders",
            Tags = new[] { "Orders" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(OrderResponse[]))]
        [ProducesResponseType(typeof(OrderResponse[]), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserOrders(
            [FromRoute, SwaggerParameter("User ID", Required = true)] int userId)
        {
            _logger.LogInformation("Fetching orders for user: {UserId}", userId);

            var orders = await _orderRepository.GetByUserIdAsync(userId);
            var orderDtos = orders.Select(MapToOrderDto).ToList();

            return Ok(orderDtos);
        }

        /// <summary>
        /// Get all orders for a specific event
        /// </summary>
        /// <param name="eventId">Event ID</param>
        /// <returns>List of orders for the event</returns>
        /// <response code="200">Returns event orders</response>
        [HttpGet("event/{eventId:int}")]
        [SwaggerOperation(
            Summary = "Get event orders",
            Description = "Retrieves all orders for a specific event",
            OperationId = "GetEventOrders",
            Tags = new[] { "Orders" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(OrderResponse[]))]
        [ProducesResponseType(typeof(OrderResponse[]), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEventOrders(
            [FromRoute, SwaggerParameter("Event ID", Required = true)] int eventId)
        {
            _logger.LogInformation("Fetching orders for event: {EventId}", eventId);

            var orders = await _orderRepository.GetByEventIdAsync(eventId);
            var orderDtos = orders.Select(MapToOrderDto).ToList();

            return Ok(orderDtos);
        }

        /// <summary>
        /// Create a new order (buy tickets)
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /v1/orders
        ///     {
        ///        "userId": 1,
        ///        "eventId": 25,
        ///        "seatIds": [3121, 3122, 3123]
        ///     }
        ///
        /// This endpoint orchestrates the complete booking workflow:
        /// 1. Validates event availability (must be ON_SALE)
        /// 2. Reserves selected seats (15-minute hold)
        /// 3. Calculates total with 5% tax
        /// 4. Processes payment
        /// 5. Confirms order and generates tickets
        /// 
        /// If payment fails, seats are automatically released.
        /// </remarks>
        /// <param name="request">Order creation request</param>
        /// <returns>Created order with tickets</returns>
        /// <response code="201">Order created successfully</response>
        /// <response code="400">Invalid request or business rule violation</response>
        [HttpPost]
        [SwaggerOperation(
            Summary = "Create new order",
            Description = "Creates a new order and orchestrates the complete ticket booking workflow",
            OperationId = "CreateOrder",
            Tags = new[] { "Orders" }
        )]
        [SwaggerResponse(StatusCodes.Status201Created, "Order created successfully", typeof(OrderResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid request or business rule violation", typeof(ErrorResponse))]
        [SwaggerRequestExample(typeof(CreateOrderRequest), typeof(Examples.CreateOrderRequestExample))]
        [SwaggerResponseExample(StatusCodes.Status201Created, typeof(Examples.OrderResponseExample))]
        [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(Examples.ErrorResponseExample))]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Creating order for User: {UserId}, Event: {EventId}",
                request.UserId, request.EventId);

            try
            {
                var order = await _orderOrchestrator.CreateOrderAsync(request);
                return CreatedAtAction(nameof(GetOrder), new { id = order.OrderId }, order);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Order creation failed");
                return BadRequest(new ErrorResponse(ex.Message));
            }
        }

        /// <summary>
        /// Cancel an existing order
        /// </summary>
        /// <remarks>
        /// Cancels a confirmed order and initiates the refund process.
        /// 
        /// This operation:
        /// - Releases all reserved seats
        /// - Initiates payment refund
        /// - Updates order status to CANCELLED
        /// - Invalidates all associated tickets
        /// </remarks>
        /// <param name="id">Order ID to cancel</param>
        /// <returns>Cancelled order details</returns>
        /// <response code="200">Order cancelled successfully</response>
        /// <response code="400">Order cannot be cancelled</response>
        /// <response code="404">Order not found</response>
        [HttpPost("{id:int}/cancel")]
        [SwaggerOperation(
            Summary = "Cancel order",
            Description = "Cancels an existing order and initiates refund process",
            OperationId = "CancelOrder",
            Tags = new[] { "Orders" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Order cancelled successfully", typeof(OrderResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Order cannot be cancelled", typeof(ErrorResponse))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "Order not found", typeof(ErrorResponse))]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelOrder(
            [FromRoute, SwaggerParameter("Order ID to cancel", Required = true)] int id)
        {
            _logger.LogInformation("Cancelling order: {OrderId}", id);

            try
            {
                var order = await _orderOrchestrator.CancelOrderAsync(id);
                return Ok(order);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Order cancellation failed");
                return BadRequest(new ErrorResponse(ex.Message));
            }
        }

        /// <summary>
        /// Get order statistics
        /// </summary>
        /// <returns>Statistical summary of all orders</returns>
        /// <response code="200">Returns order statistics</response>
        [HttpGet("statistics")]
        [SwaggerOperation(
            Summary = "Get order statistics",
            Description = "Retrieves aggregate statistics across all orders including counts by status and revenue metrics",
            OperationId = "GetOrderStatistics",
            Tags = new[] { "Orders", "Analytics" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Success")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatistics()
        {
            var allOrders = await _orderRepository.GetAllAsync(1, int.MaxValue);

            var stats = new
            {
                totalOrders = allOrders.Count,
                createdOrders = allOrders.Count(o => o.Status == "CREATED"),
                confirmedOrders = allOrders.Count(o => o.Status == "CONFIRMED"),
                cancelledOrders = allOrders.Count(o => o.Status == "CANCELLED"),
                totalRevenue = allOrders.Where(o => o.Status == "CONFIRMED").Sum(o => o.OrderTotal),
                averageOrderValue = allOrders.Any() ? allOrders.Average(o => o.OrderTotal) : 0
            };

            return Ok(stats);
        }

        private OrderResponse MapToOrderDto(Core.Entities.Order order)
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
    }
}
