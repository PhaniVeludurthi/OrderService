using Microsoft.Extensions.Logging;
using OrderService.Core.Entities;
using OrderService.Core.Interfaces;
using System.Net.Http.Json;

namespace OrderService.Infrastructure.ExternalClient
{
    public class NotificationClient(HttpClient httpClient, ILogger<NotificationClient> logger) : INotificationClient
    {
        private readonly ILogger<NotificationClient> _logger = logger;
        private readonly HttpClient _httpClient = httpClient;
        public async Task SendEventAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new NotificationEventRequest
                {
                    EventId = outboxEvent.Id,
                    AggregateType = outboxEvent.AggregateType,
                    AggregateId = outboxEvent.AggregateId,
                    EventType = outboxEvent.EventType,
                    EventPayload = outboxEvent.EventPayloadJson,
                    CorrelationId = outboxEvent.CorrelationId,
                    CreatedAt = outboxEvent.CreatedAt
                };

                var response = await _httpClient.PostAsJsonAsync(
                    "/api/notifications/events",
                    request,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                _logger.LogInformation(
                    "Successfully sent event {EventId} of type {EventType} to notification API",
                    outboxEvent.Id,
                    outboxEvent.EventType);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "HTTP error sending event {EventId} to notification API: {Message}",
                    outboxEvent.Id,
                    ex.Message);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex,
                    "Timeout sending event {EventId} to notification API",
                    outboxEvent.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error sending event {EventId} to notification API: {Message}",
                    outboxEvent.Id,
                    ex.Message);
                throw;
            }
        }
    }
}
