namespace OrderService.Core.Constants
{
    public static class OrderStatus
    {
        public const string CREATED = "CREATED";
        public const string CONFIRMED = "CONFIRMED";
        public const string CANCELLED = "CANCELLED";
        public const string PAYMENT_COMPLETED_BUT_FULFILLMENT_FAILED = "PAYMENT_COMPLETED_BUT_FULFILLMENT_FAILED";
    }
}
