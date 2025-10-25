using Microsoft.Extensions.Logging;
using OrderService.Core.Interfaces;

namespace OrderService.Infrastructure.ExternalClient
{
    public class CatalogClient(HttpClient httpClient, ILogger<CatalogClient> logger) : ICatalogClient
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ILogger<CatalogClient> _logger = logger;
        private readonly List<EventDto> _mockEvents = GenerateMockEvents();
        private readonly List<SeatDto> _mockSeats = GenerateMockSeats();
        public async Task<EventDto> GetEventAsync(int eventId)
        {
            try
            {
                _logger.LogInformation("Fetching event details for EventId: {EventId}", eventId);

                await Task.Delay(100);
                var eventDto = _mockEvents.FirstOrDefault(e => e.EventId == eventId);

                if (eventDto == null)
                {
                    _logger.LogWarning("[MOCK] Event not found: {EventId}", eventId);
                    return null;
                }
                return eventDto;

                //var response = await _httpClient.GetAsync($"/v1/events/{eventId}");

                //if (!response.IsSuccessStatusCode)
                //{
                //    _logger.LogError("Failed to fetch event. Status: {StatusCode}", response.StatusCode);
                //    return null;
                //}

                //var eventDto = await response.Content.ReadFromJsonAsync<EventDto>();
                //return eventDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Catalog Service for EventId: {EventId}", eventId);
                throw;
            }
        }

        public async Task<List<SeatDto>> GetSeatsAsync(List<int> seatIds)
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
