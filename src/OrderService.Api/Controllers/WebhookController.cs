using Microsoft.AspNetCore.Mvc;
using OrderService.Core.Interfaces;

namespace OrderService.Api.Controllers
{
    [ApiController]
    [Route("api/webhooks")]
    public class EventWebhookController(
        IOrderOrchestrator orderOrchestrator,
        ILogger<EventWebhookController> logger) : ControllerBase
    {
        private readonly IOrderOrchestrator _orderOrchestrator = orderOrchestrator;
        private readonly ILogger<EventWebhookController> _logger = logger;

        [HttpPost("event-cancelled")]
        public async Task<IActionResult> HandleEventCancelled([FromBody] EventCancelledWebhook webhook)
        {
            _logger.LogInformation(
                "Received event cancellation webhook: EventId={EventId}, CancelledAt={CancelledAt}",
                webhook.EventId, webhook.CancelledAt);

            try
            {
                await _orderOrchestrator.HandleEventCancelledAsync(webhook.EventId);

                return Ok(new { message = "Event cancellation processed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process event cancellation: EventId={EventId}",
                    webhook.EventId);

                return StatusCode(500, new { error = "Failed to process cancellation" });
            }
        }
    }

    public class EventCancelledWebhook
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public DateTime CancelledAt { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
