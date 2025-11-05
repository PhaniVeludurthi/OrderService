using OrderService.Core.Entities;

namespace OrderService.Core.Interfaces
{
    public interface INotificationClient
    {
        Task SendEventAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default);
    }

    // DTOs for the API
    public class NotificationEventRequest
    {
        public Guid EventId { get; set; }
        public string AggregateType { get; set; }
        public string AggregateId { get; set; }
        public string EventType { get; set; }
        public string EventPayload { get; set; }
        public string CorrelationId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
