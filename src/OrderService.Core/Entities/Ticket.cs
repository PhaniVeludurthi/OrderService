namespace OrderService.Core.Entities
{
    public class Ticket
    {
        public int TicketId { get; set; }
        public int OrderId { get; set; }
        public int EventId { get; set; }
        public string SeatId { get; set; }

        public decimal PricePaid { get; set; }

        // Navigation property
        public Order Order { get; set; } = null!;
    }
}
