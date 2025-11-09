namespace OrderService.Core.Constants
{
    public static class PaymentStatus
    {
        public const string SUCCESS = "SUCCESS";
        public const string PENDING = "PENDING";
        public const string FAILED = "FAILED";
        public const string REFUNDED = "REFUNDED";
    }
    public class EventStatus
    {
        public const string ONSALE = "ON_SALE";
        public const string CANCELLED = "CANCELLED";
        public static string SOLD_OUT = "SOLD_OUT";
    }
}
