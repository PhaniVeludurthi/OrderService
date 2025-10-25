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
                EventId = 25,
                SeatIds = new List<int> { 3121, 3122, 3123 }
            };
        }
    }
}
