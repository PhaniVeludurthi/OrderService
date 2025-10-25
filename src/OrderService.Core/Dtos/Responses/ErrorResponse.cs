namespace OrderService.Core.Dtos.Responses
{
    public class ErrorResponse
    {
        public string Message { get; set; }
        public string CorrelationId { get; set; }

        public ErrorResponse(string message) => Message = message;

        public ErrorResponse(string message, string correlationId)
        {
            Message = message;
            CorrelationId = correlationId;
        }
    }
}
