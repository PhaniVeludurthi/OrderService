namespace OrderService.Core.Interfaces
{
    public interface IPaymentClient
    {
        Task<PaymentResult> ChargeAsync(PaymentRequest request);
        Task<PaymentResult> RefundAsync(RefundRequest refundRequest);
    }

    public class PaymentRequest
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string IdempotencyKey { get; set; }
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public int PaymentId { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string TransactionReference { get; set; }
    }
    public class RefundRequest
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; }
    }
}
