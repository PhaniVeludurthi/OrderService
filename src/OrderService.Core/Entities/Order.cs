namespace OrderService.Core.Entities
{
    public class Order
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public int EventId { get; set; }

        // Match CSV column names
        public string? Status { get; set; }  // CREATED, CONFIRMED, CANCELLED
        public string? PaymentStatus { get; set; }  // SUCCESS, PENDING, FAILED
        public decimal OrderTotal { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? IdempotencyKey { get; set; }

        // Navigation properties
        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

        // Domain methods
        public void ConfirmOrder()
        {
            if (Status != "CREATED")
                throw new InvalidOperationException($"Cannot confirm order in {Status} status");

            Status = "CONFIRMED";
        }

        public void CancelOrder()
        {
            if (Status == "CANCELLED")
                throw new InvalidOperationException("Order already cancelled");

            Status = "CANCELLED";
        }

        public bool IsConfirmed => Status == "CONFIRMED";
        public bool IsCancelled => Status == "CANCELLED";
        public bool IsPaymentSuccessful => PaymentStatus == "SUCCESS";
    }
}
