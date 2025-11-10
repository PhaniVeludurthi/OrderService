using Microsoft.Extensions.Logging;
using OrderService.Core.Interfaces;
using System.Net.Http.Json;

namespace OrderService.Infrastructure.ExternalClient
{
    public class PaymentClient(HttpClient httpClient, ILogger<PaymentClient> logger) : IPaymentClient
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ILogger<PaymentClient> _logger = logger;
        private readonly Random _random = new Random();
        public async Task<PaymentResult> ChargeAsync(PaymentRequest request)
        {
            try
            {
                _logger.LogInformation("Processing payment for Amount: {Amount}", request.Amount);

                //await Task.Delay(300);
                //// Simulate 95% success rate
                //var isSuccess = _random.NextDouble() < 0.95;

                //if (isSuccess)
                //{
                //    var paymentId = _random.Next(1000, 9999);
                //    var reference = $"PAY-MOCK-{DateTime.UtcNow:yyyyMMddHHmmss}-{paymentId}";

                //    _logger.LogInformation("Payment successful. Reference: {Reference}", reference);

                //    return new PaymentResult
                //    {
                //        Success = true,
                //        PaymentId = paymentId,
                //        Status = "SUCCESS",
                //        Message = "Payment processed successfully",
                //        TransactionReference = reference
                //    };
                //}
                //else
                //{
                //    _logger.LogWarning("Payment failed - Simulated failure");

                //    var failureReasons = new[]
                //    {
                //        "Insufficient funds",
                //        "Card declined",
                //        "Payment gateway timeout",
                //        "Invalid card details"
                //    };

                //    var reason = failureReasons[_random.Next(failureReasons.Length)];

                //    return new PaymentResult
                //    {
                //        Success = false,
                //        Status = "FAILED",
                //        Message = reason
                //    };
                //}

                var response = await _httpClient.PostAsJsonAsync("/v1/payments/charge", request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Payment failed. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorContent);

                    return new PaymentResult
                    {
                        Success = false,
                        Status = "FAILED",
                        Message = errorContent
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<PaymentResult>();
                return result ?? new PaymentResult { Success = false, Status = "FAILED" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Payment Service");
                return new PaymentResult
                {
                    Success = false,
                    Status = "FAILED",
                    Message = $"Service unavailable: {ex.Message}"
                };
            }
        }

        public async Task<PaymentResult> RefundAsync(RefundRequest refundRequest)
        {
            try
            {
                _logger.LogInformation("Refunding Order: {OrderId}", refundRequest.OrderId);
                await Task.Delay(200); // Simulate refund processing

                // Simulate 98% success rate for refunds
                var isSuccess = _random.NextDouble() < 0.98;

                if (isSuccess)
                {
                    _logger.LogInformation("Refund successful for Order: {OrderId}", refundRequest.OrderId);
                }
                else
                {
                    _logger.LogWarning("Refund failed for Order: {OrderId}", refundRequest.OrderId);
                }

                return new PaymentResult { Success = isSuccess, Message = isSuccess ? "" : "Unkown Error" };
                //var response = await _httpClient.PostAsync($"/v1/payments/{OrderId}/refund", null);
                //return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Payment Service for refund");
                return new PaymentResult
                {
                    Success = false,
                    Message = ex.Message,
                };
            }
        }
    }
}
