using Microsoft.Extensions.Logging;
using OrderService.Core.Interfaces;

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

                await Task.Delay(300);
                // Simulate 95% success rate
                var isSuccess = _random.NextDouble() < 0.95;

                if (isSuccess)
                {
                    var paymentId = _random.Next(1000, 9999);
                    var reference = $"PAY-MOCK-{DateTime.UtcNow:yyyyMMddHHmmss}-{paymentId}";

                    _logger.LogInformation("[MOCK] Payment successful. Reference: {Reference}", reference);

                    return new PaymentResult
                    {
                        Success = true,
                        PaymentId = paymentId,
                        Status = "SUCCESS",
                        Message = "Payment processed successfully",
                        TransactionReference = reference
                    };
                }
                else
                {
                    _logger.LogWarning("[MOCK] Payment failed - Simulated failure");

                    var failureReasons = new[]
                    {
                    "Insufficient funds",
                    "Card declined",
                    "Payment gateway timeout",
                    "Invalid card details"
                };

                    var reason = failureReasons[_random.Next(failureReasons.Length)];

                    return new PaymentResult
                    {
                        Success = false,
                        Status = "FAILED",
                        Message = reason
                    };
                }

                //var response = await _httpClient.PostAsJsonAsync("/v1/payments/charge", request);

                //if (!response.IsSuccessStatusCode)
                //{
                //    var errorContent = await response.Content.ReadAsStringAsync();
                //    _logger.LogError("Payment failed. Status: {StatusCode}, Error: {Error}",
                //        response.StatusCode, errorContent);

                //    return new PaymentResult
                //    {
                //        Success = false,
                //        Status = "FAILED",
                //        Message = errorContent
                //    };
                //}

                //var result = await response.Content.ReadFromJsonAsync<PaymentResult>();
                //return result ?? new PaymentResult { Success = false, Status = "FAILED" };
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

        public async Task<bool> RefundAsync(int paymentId)
        {
            try
            {
                _logger.LogInformation("Refunding payment: {PaymentId}", paymentId);
                await Task.Delay(200); // Simulate refund processing

                // Simulate 98% success rate for refunds
                var isSuccess = _random.NextDouble() < 0.98;

                if (isSuccess)
                {
                    _logger.LogInformation("[MOCK] Refund successful for payment: {PaymentId}", paymentId);
                }
                else
                {
                    _logger.LogWarning("[MOCK] Refund failed for payment: {PaymentId}", paymentId);
                }

                return isSuccess;
                //var response = await _httpClient.PostAsync($"/v1/payments/{paymentId}/refund", null);
                //return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Payment Service for refund");
                return false;
            }
        }
    }
}
