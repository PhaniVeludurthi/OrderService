using OrderService.Core.Dtos.Requests;
using Swashbuckle.AspNetCore.Filters;

namespace OrderService.Api.Examples
{
    public class CreateOrderRequestExample : IExamplesProvider<CreateOrderRequest>
    {
        public CreateOrderRequest GetExamples()
        {
            return new CreateOrderRequest
            {
                UserId = 1,
                EventId = 33,
                SeatIds = ["4086", "4093", "4084"],
                IdempotencyKey = Guid.NewGuid().ToString(),
            };
        }
    }
}
