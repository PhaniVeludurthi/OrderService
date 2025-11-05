namespace OrderService.Core.Interfaces
{
    public interface ICorrelationService
    {
        public string GetCorrelationId();
        public void SetCorrelationId(string correlationId);
    }
}
