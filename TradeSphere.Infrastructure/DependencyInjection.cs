using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Infrastructure.Persistence;
using TradeSphere.Infrastructure.Repositories;

namespace TradeSphere.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Temporarily comment out ALL database-dependent services for JSON testing
            // Uncomment these when migrating to pgAdmin
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IAuthService, Identity.AuthService>();
            services.AddScoped<IDashboardService, Services.DashboardService>();
            services.AddScoped<IExchangeService, Services.ExchangeService>();
            services.AddScoped<IStrategyService, Services.StrategyService>();
            services.AddScoped<ITradingService, Services.TradingService>();
            services.AddScoped<IBacktestService, Services.BacktestService>();
            services.AddScoped<IMt5Service, Services.Mt5Service>();
            services.AddScoped<IPropFirmService, Services.PropFirmService>();
            services.AddHttpClient<IMt5BridgeClient, Services.Mt5BridgeClient>();
            
            // Register Delta Exchange REST Client
            services.AddHttpClient<IDeltaExchangeClient, Services.DeltaExchangeClient>();

            return services;
        }
    }
}
