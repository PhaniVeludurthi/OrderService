using OrderService.Core.Interfaces;
using Serilog.Context;

namespace OrderService.Api.Middleware
{
    public class CorrelationIdMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;
        private const string CorrelationIdHeader = "X-Correlation-ID";

        public async Task InvokeAsync(HttpContext context, ICorrelationService correlationService)
        {
            var correlationId = context.Request.Headers[CorrelationIdHeader].ToString();

            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }
            // Set the correlation ID for this request
            correlationService.SetCorrelationId(correlationId);

            context.Response.Headers.Append(CorrelationIdHeader, correlationId);

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }
}
