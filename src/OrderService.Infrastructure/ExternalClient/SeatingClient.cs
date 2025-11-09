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

                var result = await response.Content.ReadFromJsonAsync<SeatingResponseObject<ReserveSeatsResponse>>();
                if (result == null || result.Data == null)
                {
                    return new ReservationResult
                    {
                        Success = false,
                        Message = "Unkown Error"
                    };
                }

                var reservationResult = new ReservationResult()
                {
                    Success = true,
                    Message = result.Status ?? "",
                    ReservedSeats = result.Data.Seats
                };
                return reservationResult;
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
    }
}
