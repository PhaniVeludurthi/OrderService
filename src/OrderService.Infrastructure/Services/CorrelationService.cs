using OrderService.Core.Interfaces;

namespace OrderService.Infrastructure.Services
{
    public class CorrelationService : ICorrelationService
    {
        private static readonly AsyncLocal<string> _correlationId = new AsyncLocal<string>();

        public string GetCorrelationId()
        {
            if (_correlationId.Value != null)
            {
                return _correlationId.Value;
            }

            var newId = Guid.NewGuid().ToString();
            SetCorrelationId(newId);
            return newId;
        }

        public void SetCorrelationId(string correlationId)
        {
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                throw new ArgumentException("Correlation ID cannot be null or empty.", nameof(correlationId));
            }

            _correlationId.Value = correlationId;
        }
    }
}
