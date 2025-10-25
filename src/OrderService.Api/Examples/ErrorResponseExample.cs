using OrderService.Core.Dtos.Responses;
using Swashbuckle.AspNetCore.Filters;

namespace OrderService.Api.Examples
{
    public class ErrorResponseExample : IExamplesProvider<ErrorResponse>
    {
        public ErrorResponse GetExamples()
        {
            return new ErrorResponse("Event is SOLD_OUT, not available for booking")
            {
                CorrelationId = "abc-123-def-456"
            };
        }
    }
}
