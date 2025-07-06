
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradeNetics.Shared.Models;

namespace TradeNetics.WebApp.Data
{
    public class TradeHistoryService
    {
        public Task<List<TradeRecord>> GetTradesAsync()
        {
            // In a real application, you would get the trades from a database.
            // For now, we'll just return some mock data.
            var trades = new List<TradeRecord>
            {
                new TradeRecord { ExecutedAt = DateTime.Now.AddDays(-1), Symbol = "BTC/USDT", Side = "Buy", Price = 50000, Quantity = 0.1m },
                new TradeRecord { ExecutedAt = DateTime.Now.AddDays(-2), Symbol = "ETH/USDT", Side = "Sell", Price = 4000, Quantity = 1m },
                new TradeRecord { ExecutedAt = DateTime.Now.AddDays(-3), Symbol = "BTC/USD", Side = "Buy", Price = 51000, Quantity = 0.2m },
            };

            return Task.FromResult(trades);
        }
    }
}
