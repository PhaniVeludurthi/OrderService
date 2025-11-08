using OrderService.Core.Interfaces;

namespace OrderService.Infrastructure.Handler
{
    public class CorrelationHandler(ICorrelationService correlationIdProvider) : DelegatingHandler
    {
        private readonly ICorrelationService _correlationIdProvider = correlationIdProvider;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var correlationId = _correlationIdProvider.GetCorrelationId();

            // Add correlation header only if not already there
            if (!request.Headers.Contains("X-Correlation-ID"))
            {
                request.Headers.Add("X-Correlation-ID", correlationId);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

}
