using Microsoft.EntityFrameworkCore;
using OrderService.Core.Entities;
using OrderService.Core.Interfaces;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Repository
{
    internal class OutboxRepository(OrderDbContext dbContext) : IOutboxRepository
    {
        private readonly OrderDbContext _dbContext = dbContext;
        public async Task<List<OutboxEvent>> FetchUndispatchedAsync()
        {
            return await _dbContext.OutboxEvents.AsNoTracking().Where(x => !x.Dispatched).ToListAsync();
        }

        public async Task UpdateAsync(OutboxEvent evt)
        {
            _dbContext.OutboxEvents.Update(evt);
            await _dbContext.SaveChangesAsync();
        }

        public async Task SaveEventAsync(OutboxEvent outboxEvent)
        {
            outboxEvent.Dispatched = false;
            await _dbContext.OutboxEvents.AddAsync(outboxEvent);
            await _dbContext.SaveChangesAsync();
        }
    }
}
