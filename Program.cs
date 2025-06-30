using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradeNetics.Services;
using TradeNetics.Data;
using TradeNetics.Models;
using TradeNetics.Interfaces;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();

        // Ensure database is created
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TradingContext>();
            await context.Database.EnsureCreatedAsync();
        }

        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                var tradingConfig = new TradingConfiguration();
                context.Configuration.GetSection("Trading").Bind(tradingConfig);

                // Ensure API keys are loaded from environment variables
                tradingConfig.ApiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY") 
                    ?? throw new InvalidOperationException("Binance API key not found in environment variables.");
                tradingConfig.ApiSecret = Environment.GetEnvironmentVariable("BINANCE_API_SECRET")
                    ?? throw new InvalidOperationException("Binance API secret not found in environment variables.");

                services.AddSingleton(tradingConfig);

                // Database
                services.AddDbContext<TradingContext>(options =>
                    options.UseNpgsql(context.Configuration.GetConnectionString("DefaultConnection")));

                // Services
                services.AddScoped<IMarketDataRepository, MarketDataRepository>();
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
