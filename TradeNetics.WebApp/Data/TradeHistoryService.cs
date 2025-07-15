using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradeNetics.Shared.Models;

namespace TradeNetics.WebApp.Data
{
    public class TradeHistoryService
    {
        private readonly ICryptoDataService _cryptoDataService;

        public TradeHistoryService(ICryptoDataService cryptoDataService)
        {
            _cryptoDataService = cryptoDataService;
        }

        public async Task<List<TradeData>> GetTradesAsync()
        {
            return await _cryptoDataService.GetRecentTradesAsync();
        }

        public async Task<List<TradeData>> GetTradeHistoryBySymbolAsync(string symbol)
        {
            var allTrades = await _cryptoDataService.GetRecentTradesAsync();
            return allTrades.Where(t => t.Symbol.Contains(symbol, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public async Task<decimal> GetTotalPnLAsync()
        {
            var trades = await _cryptoDataService.GetRecentTradesAsync();
            return trades.Sum(t => t.PnL);
        }

        public async Task<decimal> GetDailyPnLAsync()
        {
            var trades = await _cryptoDataService.GetRecentTradesAsync();
            var today = DateTime.UtcNow.Date;
            return trades.Where(t => t.ExecutedAt.Date == today).Sum(t => t.PnL);
        }

        public async Task<int> GetTotalTradesCountAsync()
        {
            var trades = await _cryptoDataService.GetRecentTradesAsync();
            return trades.Count;
        }

        public async Task<int> GetDailyTradesCountAsync()
        {
            var trades = await _cryptoDataService.GetRecentTradesAsync();
            var today = DateTime.UtcNow.Date;
            return trades.Count(t => t.ExecutedAt.Date == today);
        }
    }
}