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
        private readonly List<EventDto> _mockEvents = GenerateMockEvents();
        private readonly List<SeatDto> _mockSeats = GenerateMockSeats();
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
        public async Task<List<SeatDto>?> GetSeatsAsync(List<int> seatIds)
        {
            try
            {
                _logger.LogInformation("Fetching seat details for {Count} seats", seatIds.Count);

                await Task.Delay(100);
                var seats = _mockSeats.Where(s => seatIds.Contains(s.SeatId)).ToList();

                if (seats.Count != seatIds.Count)
                {
                    _logger.LogWarning("[MOCK] Some seats not found. Requested: {Requested}, Found: {Found}",
                        seatIds.Count, seats.Count);
                }

                return seats;

                //var queryString = string.Join("&", seatIds.Select(id => $"seatIds={id}"));
                //var response = await _httpClient.GetAsync($"/v1/seats?{queryString}");

                //if (!response.IsSuccessStatusCode)
                //{
                //    _logger.LogError("Failed to fetch seats. Status: {StatusCode}", response.StatusCode);
                //    return new List<SeatDto>();
                //}

                //var seats = await response.Content.ReadFromJsonAsync<List<SeatDto>>();
                //return seats ?? new List<SeatDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Catalog Service for seats");
                throw;
            }
        }
        private static List<EventDto> GenerateMockEvents()
        {
            var events = new List<EventDto>();

            // Generate mock events from CSV data (sample events)
            var eventTypes = new[] { "Concert", "Play", "Sports", "Conference", "Workshop", "Stand-up" };
            var statuses = new[] { "ON_SALE", "SOLD_OUT", "SCHEDULED", "CANCELLED" };
            var cities = new[] { "Mumbai", "Delhi", "Bengaluru", "Pune", "Kolkata" };

            var random = new Random(42); // Fixed seed for consistency

            for (int i = 1; i <= 60; i++)
            {
                events.Add(new EventDto
                {
                    EventId = i,
                    Title = $"{eventTypes[random.Next(eventTypes.Length)]} #{i}",
                    EventType = eventTypes[random.Next(eventTypes.Length)],
                    Status = i <= 20 ? "ON_SALE" : statuses[random.Next(statuses.Length)],
                    EventDate = DateTime.UtcNow.AddDays(random.Next(1, 365)),
                    VenueId = random.Next(1, 16),
                    VenueName = $"Venue {random.Next(1, 16)}",
                    City = cities[random.Next(cities.Length)],
                    BasePrice = Math.Round((decimal)(random.NextDouble() * 2000 + 500), 2)
                });
            }

            return events;
        }

        private static List<SeatDto> GenerateMockSeats()
        {
            var seats = new List<SeatDto>();
            var sections = new[] { "VIP", "A", "B", "C", "D" };
            var random = new Random(42); // Fixed seed

            // Generate sample seats for events 1-60
            int seatId = 1;
            for (int eventId = 1; eventId <= 60; eventId++)
            {
                int seatsPerEvent = random.Next(50, 200);

                for (int i = 0; i < seatsPerEvent; i++)
                {
                    var section = sections[random.Next(sections.Length)];
                    var row = random.Next(1, 31);
                    var seatNumber = random.Next(1, 51);
                    var basePrice = random.Next(500, 3000);

                    // Price varies by section
                    var sectionMultiplier = section switch
                    {
                        "VIP" => 1.5m,
                        "A" => 1.3m,
                        "B" => 1.0m,
                        "C" => 0.8m,
                        "D" => 0.6m,
                        _ => 1.0m
                    };

                    seats.Add(new SeatDto
                    {
                        SeatId = seatId++,
                        EventId = eventId,
                        Section = section,
                        Row = row.ToString(),
                        SeatNumber = $"{section}{row}-{seatNumber}",
                        Price = Math.Round(basePrice * sectionMultiplier, 2)
                    });
                }
            }

            return seats;
        }
    }

}
