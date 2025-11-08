using Prometheus;

namespace OrderService.Infrastructure.Metrics
{
    public static class BusinessMetrics
    {
        public static readonly Counter OrdersTotal = Prometheus.Metrics.CreateCounter(
            "orders_total", "Total number of orders created.");

        public static readonly Counter PaymentsFailedTotal = Prometheus.Metrics.CreateCounter(
            "payments_failed_total", "Total number of failed payments.");

        public static readonly Counter SeatReservationsFailed = Prometheus.Metrics.CreateCounter(
            "seat_reservations_failed", "Total number of failed seat reservations.");
    }
}
