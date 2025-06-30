using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradeNetics.Data;
using TradeNetics.Interfaces;
using TradeNetics.Models;

namespace TradeNetics.Services
{
    public class PortfolioManager : IPortfolioManager
    {
        private readonly ICryptoTraderService _traderService;
        private readonly TradingContext _context;
        private readonly ILogger<PortfolioManager> _logger;

        public PortfolioManager(ICryptoTraderService traderService, TradingContext context, ILogger<PortfolioManager> logger)
        {
            _traderService = traderService;
            _context = context;
            _logger = logger;
        }

        public async Task<Portfolio> GetCurrentPortfolioAsync()
        {
            try
            {
                var balances = await _traderService.GetAccountBalancesAsync();
                if (balances == null) return new Portfolio();

                var portfolio = new Portfolio
                {
                    Balances = balances.Where(b => b.Free > 0 || b.Locked > 0).ToList()
                };

                // Calculate total portfolio value in USDT
                decimal totalValue = 0;
                foreach (var balance in portfolio.Balances)
                {
                    if (balance.Asset == "USDT")
                    {
                        totalValue += balance.Total;
                    }
                    else
                    {
                        var price = await _traderService.GetPriceAsync($"{balance.Asset}USDT");
                        if (price != null)
                        {
                            totalValue += balance.Total * price.Price;
                        }
                    }
                }

                portfolio.TotalValue = totalValue;
                portfolio.DailyPnL = await CalculateDailyPnLAsync();
                portfolio.TotalPnL = await CalculatePnLAsync();

                return portfolio;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating portfolio");
                return new Portfolio();
            }
        }

        public async Task SavePortfolioSnapshotAsync()
        {
            try
            {
                var portfolio = await GetCurrentPortfolioAsync();

                var snapshot = new PortfolioSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    TotalValue = portfolio.TotalValue,
                    DailyPnL = portfolio.DailyPnL,
                    TotalPnL = portfolio.TotalPnL,
                    AssetAllocation = JsonSerializer.Serialize(portfolio.AssetAllocation),
                    RiskScore = portfolio.RiskScore
                };

                _context.PortfolioSnapshots.Add(snapshot);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving portfolio snapshot");
            }
        }

        public async Task<decimal> CalculatePnLAsync()
        {
            var trades = await _context.TradeRecords
                .Where(t => t.ExecutedAt >= DateTime.UtcNow.AddDays(-30))
                .ToListAsync();

            return trades.Sum(t => t.PnL);
        }

        private async Task<decimal> CalculateDailyPnLAsync()
        {
            var today = DateTime.UtcNow.Date;
            var trades = await _context.TradeRecords
                .Where(t => t.ExecutedAt >= today)
                .ToListAsync();

            return trades.Sum(t => t.PnL);
        }
    }
}
