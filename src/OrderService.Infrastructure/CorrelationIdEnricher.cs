using OrderService.Core.Interfaces;
using Serilog.Core;
using Serilog.Events;

namespace OrderService.Infrastructure
{
    public class CorrelationIdEnricher : ILogEventEnricher
    {
        private readonly ICorrelationService _correlationService;

        public CorrelationIdEnricher(ICorrelationService correlationService)
        {
            _correlationService = correlationService;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var correlationId = _correlationService.GetCorrelationId();
            var property = propertyFactory.CreateProperty("CorrelationId", correlationId);
            logEvent.AddPropertyIfAbsent(property);
        }
    }
}
