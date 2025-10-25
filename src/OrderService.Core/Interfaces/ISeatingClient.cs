namespace OrderService.Core.Interfaces
{
    public interface ISeatingClient
    {
        Task<ReservationResult> ReserveSeatsAsync(ReserveSeatRequest request);
        Task<bool> AllocateSeatsAsync(AllocateSeatRequest request);
        Task<bool> ReleaseSeatsAsync(ReleaseSeatRequest request);
    }

    public class ReserveSeatRequest
    {
        public int EventId { get; set; }
        public List<int> SeatIds { get; set; }
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public int TtlSeconds { get; set; } = 900; // 15 minutes
    }

    public class ReservationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<ReservedSeat> ReservedSeats { get; set; }
    }

    public class ReservedSeat
    {
        public int SeatId { get; set; }
        public decimal Price { get; set; }
    }

    public class AllocateSeatRequest
    {
        public int OrderId { get; set; }
        public int EventId { get; set; }
        public List<int> SeatIds { get; set; } = [];
    }

    public class ReleaseSeatRequest
    {
        public int OrderId { get; set; }
        public int EventId { get; set; }
        public List<int> SeatIds { get; set; } = [];
    }
}
