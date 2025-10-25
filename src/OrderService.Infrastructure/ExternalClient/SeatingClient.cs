using Microsoft.Extensions.Logging;
using OrderService.Core.Interfaces;

namespace OrderService.Infrastructure.ExternalClient
{
    public class SeatingClient(HttpClient httpClient, ILogger<SeatingClient> logger) : ISeatingClient
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ILogger<SeatingClient> _logger = logger;
        private readonly Dictionary<string, DateTime> _reservedSeats = new Dictionary<string, DateTime>();
        private readonly HashSet<string> _allocatedSeats = new HashSet<string>();


        public async Task<ReservationResult> ReserveSeatsAsync(ReserveSeatRequest request)
        {
            try
            {
                _logger.LogInformation("Reserving {Count} seats for Event: {EventId}",
                    request.SeatIds.Count, request.EventId);

                await Task.Delay(200); // Simulate network delay

                var reservedSeats = new List<ReservedSeat>();
                var random = new Random();

                foreach (var seatId in request.SeatIds)
                {
                    var key = $"{request.EventId}:{seatId}";

                    // Check if seat is already allocated
                    if (_allocatedSeats.Contains(key))
                    {
                        _logger.LogWarning("[MOCK] Seat {SeatId} is already allocated", seatId);
                        return new ReservationResult
                        {
                            Success = false,
                            Message = $"Seat {seatId} is already booked"
                        };
                    }

                    // Check if seat is reserved by another order (not expired)
                    if (_reservedSeats.ContainsKey(key))
                    {
                        if (_reservedSeats[key] > DateTime.UtcNow)
                        {
                            _logger.LogWarning("[MOCK] Seat {SeatId} is already reserved", seatId);
                            return new ReservationResult
                            {
                                Success = false,
                                Message = $"Seat {seatId} is currently reserved by another user"
                            };
                        }

                        // Remove expired reservation
                        _reservedSeats.Remove(key);
                    }

                    // Reserve the seat
                    var expiryTime = DateTime.UtcNow.AddSeconds(request.TtlSeconds);
                    _reservedSeats[key] = expiryTime;

                    // Generate mock price
                    var price = Math.Round((decimal)(random.NextDouble() * 1500 + 500), 2);

                    reservedSeats.Add(new ReservedSeat
                    {
                        SeatId = seatId,
                        Price = price
                    });
                }

                _logger.LogInformation("[MOCK] Successfully reserved {Count} seats", reservedSeats.Count);

                return new ReservationResult
                {
                    Success = true,
                    Message = "Seats reserved successfully",
                    ReservedSeats = reservedSeats
                };

                //var response = await _httpClient.PostAsJsonAsync("/v1/seats/reserve", request);

                //if (!response.IsSuccessStatusCode)
                //{
                //    var errorContent = await response.Content.ReadAsStringAsync();
                //    _logger.LogError("Seat reservation failed. Status: {StatusCode}, Error: {Error}",
                //        response.StatusCode, errorContent);

                //    return new ReservationResult
                //    {
                //        Success = false,
                //        Message = errorContent
                //    };
                //}

                //var result = await response.Content.ReadFromJsonAsync<ReservationResult>();
                //return result ?? new ReservationResult { Success = false, Message = "Unknown error" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Seating Service for reservation");
                return new ReservationResult
                {
                    Success = false,
                    Message = $"Service unavailable: {ex.Message}"
                };
            }
        }

        public async Task<bool> AllocateSeatsAsync(AllocateSeatRequest request)
        {
            try
            {
                _logger.LogInformation("Allocating seats for Event: {EventId}", request.EventId);

                await Task.Delay(150);
                return true;

                //var response = await _httpClient.PostAsJsonAsync("/v1/seats/allocate", request);
                //return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Seating Service for allocation");
                return false;
            }
        }

        public async Task<bool> ReleaseSeatsAsync(ReleaseSeatRequest request)
        {
            try
            {
                _logger.LogInformation("Releasing seats for Event: {EventId}", request.EventId);

                await Task.Delay(100); // Simulate network delay

                // Remove reservations for this event
                var keysToRemove = _reservedSeats.Keys
                    .Where(k => k.StartsWith($"{request.EventId}:"))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _reservedSeats.Remove(key);
                }
                _logger.LogInformation("[MOCK] Released {Count} seat reservations", keysToRemove.Count);

                return true;

                //var response = await _httpClient.PostAsJsonAsync("/v1/seats/release", request);
                //return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Seating Service for release");
                return false;
            }
        }
    }

}
