
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TradeNetics.WebApp.Data
{
    public class Trade
    {
        public DateTime Timestamp { get; set; }
        public string Exchange { get; set; }
        public string Symbol { get; set; }
        public string Side { get; set; }
        public double Price { get; set; }
        public double Quantity { get; set; }
    }

    public class TradeHistoryService
    {
        public Task<List<Trade>> GetTradesAsync()
        {
            // In a real application, you would get the trades from a database.
            // For now, we'll just return some mock data.
            var trades = new List<Trade>
            {
                new Trade { Timestamp = DateTime.Now.AddDays(-1), Exchange = "Binance", Symbol = "BTC/USDT", Side = "Buy", Price = 50000, Quantity = 0.1 },
                new Trade { Timestamp = DateTime.Now.AddDays(-2), Exchange = "Binance", Symbol = "ETH/USDT", Side = "Sell", Price = 4000, Quantity = 1 },
                new Trade { Timestamp = DateTime.Now.AddDays(-3), Exchange = "Coinbase", Symbol = "BTC/USD", Side = "Buy", Price = 51000, Quantity = 0.2 },
            };

            return Task.FromResult(trades);
        }
    }
}
