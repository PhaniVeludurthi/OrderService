namespace OrderService.Core.Interfaces
{
    public interface ISeatingClient
    {
        Task<ReservationResult> ReserveSeatsAsync(ReserveSeatRequest request);
        Task<bool> AllocateSeatsAsync(AllocateSeatRequest request);
        Task<bool> ReleaseSeatsAsync(ReleaseSeatRequest request);
        Task<List<SeatDto>> GetSeatsAsync(int eventId);
    }

    public class ReserveSeatRequest
    {
        public string EventId { get; set; }
        public List<string> SeatIds { get; set; }
        public string UserId { get; set; }
        public int TtlSeconds { get; set; } = 900; // 15 minutes
    }

    public class ReservationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<SeatDto> ReservedSeats { get; set; }
    }

    public class ReservedSeat
    {
        public string SeatId { get; set; }
        public decimal Price { get; set; }
    }

    public class AllocateSeatRequest
    {
        public string EventId { get; set; }
        public string UserId { get; set; }
        public List<string> SeatIds { get; set; } = [];
    }

    public class ReleaseSeatRequest
    {
        public int OrderId { get; set; }
        public string EventId { get; set; }
        public string UserId { get; set; }
        public List<string> SeatIds { get; set; } = [];
    }

    public class SeatDto
    {
        public string SeatId { get; set; }
        public string? SeatNumber { get; set; }
        public string? Section { get; set; }
        public string? Row { get; set; }
        public decimal Price { get; set; }
        public string? Status { get; set; }
        public string EventId { get; set; }
        public string? ReservedBy { get; set; }
        public DateTime? ReservedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class TickerResponseData
    {
        public string? Status { get; set; }
        public int Code { get; set; }
        public string? Message { get; set; }
        public SeatResponse Data { get; set; }
    }
    public class SeatResponse
    {
        public List<SeatDto> Seats { get; set; }
    }

    public class SeatingResponseObject<T>
    {
        public string? Status { get; set; }
        public string? Code { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    public class ReserveSeatsResponse
    {
        public List<SeatDto> Seats { get; set; } = [];
    }
}
