using Microsoft.Extensions.Logging;
using OrderService.Core.Interfaces;
using System.Net.Http.Json;

namespace OrderService.Infrastructure.ExternalClient
{
    public class CatalogClient(HttpClient httpClient, ILogger<CatalogClient> logger) : ICatalogClient
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ILogger<CatalogClient> _logger = logger;
        public async Task<EventDto?> GetEventAsync(int eventId)
        {
            try
            {
                _logger.LogInformation("Fetching event details for EventId: {EventId}", eventId);

                var response = await _httpClient.GetAsync($"/api/v1/Events/{eventId}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch event. Status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var eventDto = await response.Content.ReadFromJsonAsync<EventDto>();
                return eventDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Catalog Service for EventId: {EventId}", eventId);
                throw;
            }
        }
    }
}
