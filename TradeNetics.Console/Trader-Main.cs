using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradeNetics.Shared.Services;
using TradeNetics.Shared.Data;
using TradeNetics.Shared.Models;
using TradeNetics.Shared.Interfaces;
using TradeNetics.Console.Services;
using TradeNetics.Shared.Extensions;

public class TraderConsole
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();

        // Ensure database is created
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
            await context.Database.EnsureCreatedAsync();
        }

        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                var tradingConfig = new TradingConfiguration();
                context.Configuration.GetSection("Trading").Bind(tradingConfig);

                // Load API keys from environment variables (optional for testing)
                tradingConfig.ApiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY") 
                    ?? "test-api-key";
                tradingConfig.ApiSecret = Environment.GetEnvironmentVariable("BINANCE_API_SECRET")
                    ?? "test-api-secret";
                
                // Warn if using test keys
                if (tradingConfig.ApiKey == "test-api-key" || tradingConfig.ApiSecret == "test-api-secret")
                {
                    Console.WriteLine("WARNING: Using test API keys. Set BINANCE_API_KEY and BINANCE_API_SECRET environment variables for production.");
                }

                services.AddSingleton(tradingConfig);

                // Shared Services
                services.AddTradeNeticsSharedServices(context.Configuration);

                // Console Specific Services
                services.AddScoped<ICryptoTraderService, CryptoTraderService>();
                services.AddScoped<IRiskManager, RiskManager>();
                services.AddScoped<IPortfolioManager, PortfolioManager>();
                services.AddSingleton<IMLTradingModel, MLTradingModel>();
                services.AddScoped<BacktestEngine>();

                // Background service
                services.AddHostedService<TradingBotService>();

                // Logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddFile("logs/trading-{Date}.log");
                });
            });
}
