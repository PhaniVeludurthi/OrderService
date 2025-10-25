namespace OrderService.Core.Dtos.Responses
{
    public class OrderResponse
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public int EventId { get; set; }
        public string? Status { get; set; }
        public string? PaymentStatus { get; set; }
        public decimal OrderTotal { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<TicketResponse> Tickets { get; set; } = [];
    }
}
