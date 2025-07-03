
using System;
using System.Threading.Tasks;

namespace TradeNetics.WebApp.Data
{
    public class TradingBotStatus
    {
        public bool IsRunning { get; set; }
        public double TotalProfit { get; set; }
        public int TotalTrades { get; set; }
    }

    public class TradingBotStatusService
    {
        public Task<TradingBotStatus> GetStatusAsync()
        {
            // In a real application, you would get the status from the trading bot.
            // For now, we'll just return some mock data.
            return Task.FromResult(new TradingBotStatus
            {
                IsRunning = true,
                TotalProfit = 1234.56,
                TotalTrades = 123
            });
        }
    }
}
