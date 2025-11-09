namespace OrderService.Core.Events
{
    public class OrderConfirmedEvent
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public int EventId { get; set; }
        public string? EventTitle { get; set; }
        public List<string> SeatIds { get; set; }
        public decimal OrderTotal { get; set; }
        public DateTime ConfirmedAt { get; set; }
        public string CorrelationId { get; set; }
    }

    public class OrderCancelledEvent
    {
        public int OrderId { get; set; }
        public string Reason { get; set; }
        public DateTime CancelledAt { get; set; }
        public string CorrelationId { get; set; }
    }
    public class OrderRefundedEvent
    {
        public int OrderId { get; set; }
        public int EventId { get; set; }
        public int UserId { get; set; }
        public decimal RefundAmount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime RefundedAt { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }
}
