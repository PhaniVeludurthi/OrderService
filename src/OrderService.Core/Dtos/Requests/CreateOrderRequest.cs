using System.ComponentModel.DataAnnotations;

namespace OrderService.Core.Dtos.Requests
{
    public class CreateOrderRequest
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public int EventId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one seat must be selected")]
        public List<int> SeatIds { get; set; }
        public string? IdempotencyKey { get; set; }
    }
}
