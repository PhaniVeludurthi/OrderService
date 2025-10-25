namespace OrderService.Core.Interfaces
{
    public interface ICatalogClient
    {
        Task<EventDto?> GetEventAsync(int eventId);
        Task<List<SeatDto>?> GetSeatsAsync(List<int> seatIds);
    }

    public class EventDto
    {
        public int EventId { get; set; }
        public string? Title { get; set; }
        public string? Status { get; set; }
        public DateTime EventDate { get; set; }
        public string EventType { get; set; }
        public int VenueId { get; set; }
        public string VenueName { get; set; }
        public string City { get; set; }
        public decimal BasePrice { get; set; }
    }

    public class SeatDto
    {
        public int SeatId { get; set; }
        public string? SeatNumber { get; set; }
        public string? Section { get; set; }
        public string? Row { get; set; }
        public decimal Price { get; set; }
        public int EventId { get; set; }
    }
}
