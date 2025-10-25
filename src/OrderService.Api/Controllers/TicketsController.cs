using Microsoft.AspNetCore.Mvc;
using OrderService.Core.Dtos.Responses;
using OrderService.Core.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace OrderService.Api.Controllers
{
    [ApiController]
    [Route("v1/tickets")]
    [Produces("application/json")]
    [SwaggerTag("Ticket management endpoints for viewing and managing event tickets")]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketRepository _ticketRepository;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(ITicketRepository ticketRepository, ILogger<TicketsController> logger)
        {
            _ticketRepository = ticketRepository;
            _logger = logger;
        }

        /// <summary>
        /// Get ticket by ID
        /// </summary>
        /// <param name="id">Ticket ID</param>
        /// <returns>Ticket details</returns>
        /// <response code="200">Returns the ticket details</response>
        /// <response code="404">Ticket not found</response>
        [HttpGet("{id:int}")]
        [SwaggerOperation(
            Summary = "Get ticket by ID",
            Description = "Retrieves detailed information about a specific ticket",
            OperationId = "GetTicketById",
            Tags = new[] { "Tickets" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(TicketResponse))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "Ticket not found", typeof(ErrorResponse))]
        [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTicket(
            [FromRoute, SwaggerParameter("Unique ticket identifier", Required = true)] int id)
        {
            _logger.LogInformation("Fetching ticket: {TicketId}", id);

            var ticket = await _ticketRepository.GetByIdAsync(id);
            if (ticket == null)
            {
                _logger.LogWarning("Ticket not found: {TicketId}", id);
                return NotFound(new ErrorResponse($"Ticket {id} not found"));
            }

            var ticketDto = MapToTicketDto(ticket);
            return Ok(ticketDto);
        }

        /// <summary>
        /// Get all tickets for an order
        /// </summary>
        /// <param name="orderId">Order ID</param>
        /// <returns>List of tickets associated with the order</returns>
        /// <response code="200">Returns the list of tickets</response>
        [HttpGet("order/{orderId:int}")]
        [SwaggerOperation(
            Summary = "Get tickets by order",
            Description = "Retrieves all tickets associated with a specific order",
            OperationId = "GetTicketsByOrder",
            Tags = new[] { "Tickets" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(TicketResponse[]))]
        [ProducesResponseType(typeof(TicketResponse[]), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOrderTickets(
            [FromRoute, SwaggerParameter("Order ID", Required = true)] int orderId)
        {
            _logger.LogInformation("Fetching tickets for order: {OrderId}", orderId);

            var tickets = await _ticketRepository.GetByOrderIdAsync(orderId);

            var ticketDtos = tickets.Select(MapToTicketDto).ToList();

            _logger.LogInformation("Found {Count} tickets for order: {OrderId}", ticketDtos.Count, orderId);

            return Ok(ticketDtos);
        }

        /// <summary>
        /// Get all tickets for an event
        /// </summary>
        /// <param name="eventId">Event ID</param>
        /// <returns>List of tickets for the event</returns>
        /// <response code="200">Returns the list of tickets</response>
        [HttpGet("event/{eventId:int}")]
        [SwaggerOperation(
            Summary = "Get tickets by event",
            Description = "Retrieves all tickets for a specific event",
            OperationId = "GetTicketsByEvent",
            Tags = new[] { "Tickets" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(TicketResponse[]))]
        [ProducesResponseType(typeof(TicketResponse[]), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEventTickets(
            [FromRoute, SwaggerParameter("Event ID", Required = true)] int eventId)
        {
            _logger.LogInformation("Fetching tickets for event: {EventId}", eventId);

            var tickets = await _ticketRepository.GetByEventIdAsync(eventId);

            var ticketDtos = tickets.Select(MapToTicketDto).ToList();

            _logger.LogInformation("Found {Count} tickets for event: {EventId}", ticketDtos.Count, eventId);

            return Ok(ticketDtos);
        }

        /// <summary>
        /// Get ticket statistics by event
        /// </summary>
        /// <param name="eventId">Event ID</param>
        /// <returns>Ticket statistics for the event</returns>
        /// <response code="200">Returns ticket statistics</response>
        [HttpGet("event/{eventId:int}/statistics")]
        [SwaggerOperation(
            Summary = "Get ticket statistics by event",
            Description = "Retrieves statistical information about tickets for a specific event",
            OperationId = "GetTicketStatisticsByEvent",
            Tags = new[] { "Tickets", "Analytics" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Success")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEventTicketStatistics(
            [FromRoute, SwaggerParameter("Event ID", Required = true)] int eventId)
        {
            _logger.LogInformation("Fetching ticket statistics for event: {EventId}", eventId);

            var tickets = await _ticketRepository.GetByEventIdAsync(eventId);

            var stats = new
            {
                eventId = eventId,
                totalTickets = tickets.Count,
                totalRevenue = tickets.Sum(t => t.PricePaid),
                averagePrice = tickets.Any() ? tickets.Average(t => t.PricePaid) : 0,
                minPrice = tickets.Any() ? tickets.Min(t => t.PricePaid) : 0,
                maxPrice = tickets.Any() ? tickets.Max(t => t.PricePaid) : 0
            };

            return Ok(stats);
        }

        /// <summary>
        /// Get ticket summary for multiple events
        /// </summary>
        /// <param name="eventIds">Comma-separated event IDs</param>
        /// <returns>Ticket counts by event</returns>
        /// <response code="200">Returns ticket summary</response>
        [HttpGet("summary")]
        [SwaggerOperation(
            Summary = "Get ticket summary",
            Description = "Retrieves ticket count summary for multiple events",
            OperationId = "GetTicketSummary",
            Tags = new[] { "Tickets", "Analytics" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Success")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTicketSummary(
            [FromQuery, SwaggerParameter("Comma-separated event IDs (e.g., 1,2,3)")] string eventIds = null)
        {
            _logger.LogInformation("Fetching ticket summary");

            if (string.IsNullOrWhiteSpace(eventIds))
            {
                return Ok(new { message = "Please provide eventIds parameter" });
            }

            var eventIdList = eventIds
                .Split(',')
                .Select(id => int.TryParse(id.Trim(), out var eventId) ? eventId : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .ToList();

            if (!eventIdList.Any())
            {
                return BadRequest(new ErrorResponse("Invalid eventIds format"));
            }

            var summary = new System.Collections.Generic.List<object>();

            foreach (var eventId in eventIdList)
            {
                var tickets = await _ticketRepository.GetByEventIdAsync(eventId);
                summary.Add(new
                {
                    eventId = eventId,
                    ticketCount = tickets.Count,
                    totalRevenue = tickets.Sum(t => t.PricePaid)
                });
            }

            return Ok(summary);
        }

        // Helper method to map entity to DTO (prevents circular reference)
        private TicketResponse MapToTicketDto(Core.Entities.Ticket ticket)
        {
            return new TicketResponse
            {
                TicketId = ticket.TicketId,
                OrderId = ticket.OrderId,
                EventId = ticket.EventId,
                SeatId = ticket.SeatId,
                PricePaid = ticket.PricePaid
            };
        }
    }
}
