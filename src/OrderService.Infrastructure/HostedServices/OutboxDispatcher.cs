using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Core.Interfaces;

namespace OrderService.Infrastructure.HostedServices
{
    public class OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxDispatcher> logger) : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<OutboxDispatcher> _logger = logger;
        private Timer? _timer;

        public async Task DispatchPendingEventsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

                var events = await outboxRepository.FetchUndispatchedAsync();

                if (events.Count == 0)
                {
                    _logger.LogDebug("No pending outbox events to dispatch");
                    return;
                }

                _logger.LogInformation("Dispatching {Count} pending outbox events", events.Count());

                foreach (var evt in events)
                {
                    // Fire and forget - don't await
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var innerScope = _scopeFactory.CreateScope();
                            var scopedRepo = innerScope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                            var scopedClient = innerScope.ServiceProvider.GetRequiredService<INotificationClient>();

                            await scopedClient.SendEventAsync(evt);

                            // Mark as dispatched
                            evt.Dispatched = true;
                            await scopedRepo.UpdateAsync(evt);

                            _logger.LogInformation(
                                "Dispatched outbox event {EventId} of type {EventType}",
                                evt.Id,
                                evt.EventType);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Outbox dispatch failed for EventId={EventId}, EventType={EventType}: {Error}",
                                evt.Id,
                                evt.EventType,
                                ex.Message);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching undispatched events: {Message}", ex.Message);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("OutboxDispatcher service starting");

            _timer = new Timer(
                async _ => await DispatchPendingEventsAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromMinutes(1));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("OutboxDispatcher service stopping");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
