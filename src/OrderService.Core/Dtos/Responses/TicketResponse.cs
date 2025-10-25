namespace OrderService.Core.Dtos.Responses
{
    public class TicketResponse
    {
        public int TicketId { get; set; }
        public int OrderId { get; set; }
        public int EventId { get; set; }
        public int SeatId { get; set; }
        public decimal PricePaid { get; set; }
    }
}
