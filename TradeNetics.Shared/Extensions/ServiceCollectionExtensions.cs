using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using TradeNetics.Shared.Data;
using TradeNetics.Shared.Interfaces;
using TradeNetics.Shared.Services;

namespace TradeNetics.Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTradeNeticsSharedServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Database - Use SQLite for development, PostgreSQL for production
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            
            services.AddDbContext<TradingDbContext>(options =>
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    // Default to SQLite if no connection string
                    options.UseSqlite("Data Source=tradnetics_default.db");
                }
                else if (connectionString.Contains("Data Source") || connectionString.EndsWith(".db"))
                {
                    // SQLite connection string
                    options.UseSqlite(connectionString);
                }
                else
                {
                    // PostgreSQL connection string
                    options.UseNpgsql(connectionString);
                }
            });

            // Repositories
            services.AddScoped<IMarketDataRepository, MarketDataRepository>();
            
            // Services
            services.AddScoped<IConfigurationService, ConfigurationService>();
            
            return services;
        }
    }
}