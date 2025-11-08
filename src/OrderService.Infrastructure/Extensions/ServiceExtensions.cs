using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Core.Interfaces;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.ExternalClient;
using OrderService.Infrastructure.Handler;
using OrderService.Infrastructure.HostedServices;
using OrderService.Infrastructure.Repository;
using OrderService.Infrastructure.Services;

namespace OrderService.Infrastructure.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<OrderDbContext>(options =>
                    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<ITicketRepository, TicketRepository>();
            services.AddSingleton<ICorrelationService, CorrelationService>();
            services.AddScoped<IOutboxRepository, OutboxRepository>();

            // Orchestrator
            services.AddScoped<IOrderOrchestrator, OrderOrchestrator>();

            services.AddTransient<CorrelationHandler>();

            services.AddScoped<DatabaseSeeder>();

            // HTTP Clients for external services
            services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Services:CatalogUrl"]!);
                client.Timeout = TimeSpan.FromSeconds(30);
            }).AddHttpMessageHandler<CorrelationHandler>();

            services.AddHttpClient<ISeatingClient, SeatingClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Services:SeatingUrl"]!);
                client.Timeout = TimeSpan.FromSeconds(30);
            }).AddHttpMessageHandler<CorrelationHandler>();

            services.AddHttpClient<IPaymentClient, PaymentClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Services:PaymentUrl"]!);
                client.Timeout = TimeSpan.FromSeconds(30);
            }).AddHttpMessageHandler<CorrelationHandler>();

            services.AddHttpClient<INotificationClient, NotificationClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Services:NotificationUrl"]!);
                client.Timeout = TimeSpan.FromSeconds(30);
            }).AddHttpMessageHandler<CorrelationHandler>();

            // Health checks
            services.AddHealthChecks()
                .AddNpgSql(
                    configuration.GetConnectionString("DefaultConnection")!,
                    name: "postgres",
                    tags: ["ready", "db"]);


            services.AddHostedService<OutboxDispatcher>();

            return services;
        }
    }
}
