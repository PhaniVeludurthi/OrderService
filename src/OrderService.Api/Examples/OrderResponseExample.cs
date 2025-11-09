using OrderService.Core.Dtos.Responses;
using Swashbuckle.AspNetCore.Filters;

namespace OrderService.Api.Examples
{
    public class OrderResponseExample : IExamplesProvider<OrderResponse>
    {
        public OrderResponse GetExamples()
        {
            return new OrderResponse
            {
                OrderId = 401,
                UserId = 1,
                EventId = 25,
                Status = "CONFIRMED",
                PaymentStatus = "SUCCESS",
                OrderTotal = 3150.75m,
                CreatedAt = DateTime.UtcNow,
                Tickets = new List<TicketResponse>
                {
                    new TicketResponse
                    {
                        TicketId = 741,
                        OrderId = 401,
                        EventId = 25,
                        SeatId = "3121",
                        PricePaid = 1000.25m
                    },
                    new TicketResponse
                    {
                        TicketId = 742,
                        OrderId = 401,
                        EventId = 25,
                        SeatId = "3122",
                        PricePaid = 1000.25m
                    },
                    new TicketResponse
                    {
                        TicketId = 743,
                        OrderId = 401,
                        EventId = 25,
                        SeatId = "3123",
                        PricePaid = 1000.25m
                    }
                }
            };
        }
    }
}
