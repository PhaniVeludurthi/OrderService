using Microsoft.Extensions.Logging;
using OrderService.Core.Interfaces;
using System.Net.Http.Json;

namespace OrderService.Infrastructure.ExternalClient
{
    public class SeatingClient(HttpClient httpClient, ILogger<SeatingClient> logger) : ISeatingClient
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ILogger<SeatingClient> _logger = logger;
        public async Task<ReservationResult> ReserveSeatsAsync(ReserveSeatRequest request)
        {
            try
            {
                _logger.LogInformation("Reserving {Count} seats for Event: {EventId}",
                    request.SeatIds.Count, request.EventId);

                var response = await _httpClient.PostAsJsonAsync("/v1/seats/reserve", request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Seat reservation failed. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorContent);

                    return new ReservationResult
                    {
                        Success = false,
                        Message = errorContent
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<ReservationResult>();
                return result ?? new ReservationResult { Success = false, Message = "Unknown error" };
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

                var response = await _httpClient.PostAsJsonAsync("/v1/seats/allocate", request);
                return response.IsSuccessStatusCode;
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

                var response = await _httpClient.PostAsJsonAsync("/v1/seats/release", request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Seating Service for release");
                return false;
            }
        }
        public async Task<List<SeatDto>> GetSeatsAsync(int eventId)
        {
            try
            {
                _logger.LogInformation("Fetching seat details for event {Count}", eventId);

                var response = await _httpClient.GetAsync($"/v1/seats?eventId={eventId}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch seats. Status: {StatusCode}", response.StatusCode);
                    return [];
                }

                var result = await response.Content.ReadFromJsonAsync<TickerResponseData>();
                var seats = result == null ? [] : result?.Data.Seats ?? [];
                return seats;
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
                        SeatId = Guid.NewGuid().ToString(),
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
