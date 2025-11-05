namespace OrderService.Core.Entities
{
    public class OutboxEvent
    {
        public Guid Id { get; set; }
        public string AggregateType { get; set; } // e.g. "Order"
        public string AggregateId { get; set; }
        public string EventType { get; set; }
        public string EventPayloadJson { get; set; }
        public string CorrelationId { get; set; } // for distributed tracing
        public DateTime CreatedAt { get; set; }
        public bool Dispatched { get; set; } = false; // mark when published
    }
}
