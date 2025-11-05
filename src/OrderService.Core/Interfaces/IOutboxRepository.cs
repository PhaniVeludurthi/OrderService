using OrderService.Core.Entities;

namespace OrderService.Core.Interfaces
{
    public interface IOutboxRepository
    {
        public Task SaveEventAsync(OutboxEvent outboxEvent);
        public Task<List<OutboxEvent>> FetchUndispatchedAsync();
        Task UpdateAsync(OutboxEvent evt);
    }
}
