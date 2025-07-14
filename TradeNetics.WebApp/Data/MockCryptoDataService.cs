using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradeNetics.Shared.Models;

namespace TradeNetics.WebApp.Data
{
    public class MockCryptoDataService : ICryptoDataService
    {
        private readonly Random _random = new();
        private readonly Dictionary<string, decimal> _basePrices = new()
        {
            { "BTCUSDT", 120850.75m },  // Updated to current BTC range
            { "ETHUSDT", 3040.30m },    // Updated to current ETH range  
            { "ADAUSDT", 0.765m },      // Updated to current ADA range
            { "LTCUSDT", 135.45m },     // Updated to current LTC range
            { "DOGEUSDT", 0.389m }      // Updated to current DOGE range
        };

        public async Task<List<MarketData>> GetMarketDataAsync()
        {
            await Task.Delay(100); // Simulate API call
            
            var marketData = new List<MarketData>();
            
            foreach (var pair in _basePrices)
            {
                var priceVariation = (decimal)(_random.NextDouble() * 0.06 - 0.03); // ±3% variation
                var currentPrice = pair.Value * (1 + priceVariation);
                var change24h = (decimal)(_random.NextDouble() * 8 - 4); // ±4% daily change
                
                // Make volume proportional to price (higher value coins have higher volume)
                var baseVolume = pair.Key switch
                {
                    "BTCUSDT" => 2000000000m,
                    "ETHUSDT" => 1500000000m,
                    "ADAUSDT" => 800000000m,
                    "LTCUSDT" => 400000000m,
                    "DOGEUSDT" => 1200000000m,
                    _ => 500000000m
                };
                
                marketData.Add(new MarketData
                {
                    Symbol = pair.Key,
                    Close = Math.Round(currentPrice, pair.Key.Contains("USDT") && currentPrice < 1 ? 6 : 2),
                    PriceChange24h = Math.Round(change24h, 2),
                    VolumeChange24h = Math.Round(baseVolume * (decimal)(_random.NextDouble() * 0.4 + 0.8), 0), // ±20% volume variation
                    Timestamp = DateTime.UtcNow,
                    Open = Math.Round(currentPrice * (1 - change24h / 100), pair.Key.Contains("USDT") && currentPrice < 1 ? 6 : 2),
                    High = Math.Round(currentPrice * 1.025m, pair.Key.Contains("USDT") && currentPrice < 1 ? 6 : 2),
                    Low = Math.Round(currentPrice * 0.975m, pair.Key.Contains("USDT") && currentPrice < 1 ? 6 : 2),
                    Volume = Math.Round(baseVolume * (decimal)(_random.NextDouble() * 0.4 + 0.8), 0)
                });
            }
            
            return marketData;
        }

        public async Task<List<PortfolioHolding>> GetPortfolioHoldingsAsync()
        {
            await Task.Delay(50);
            
            var holdings = new List<PortfolioHolding>
            {
                new PortfolioHolding
                {
                    Symbol = "BTC",
                    Name = "Bitcoin",
                    Amount = 0.1825m + (decimal)(_random.NextDouble() * 0.01 - 0.005), // Small variation
                    Price = _basePrices["BTCUSDT"] * (1 + (decimal)(_random.NextDouble() * 0.02 - 0.01)),
                    Change24h = (decimal)(_random.NextDouble() * 6 - 3) // ±3%
                },
                new PortfolioHolding
                {
                    Symbol = "ETH", 
                    Name = "Ethereum",
                    Amount = 1.45m + (decimal)(_random.NextDouble() * 0.1 - 0.05),
                    Price = _basePrices["ETHUSDT"] * (1 + (decimal)(_random.NextDouble() * 0.02 - 0.01)),
                    Change24h = (decimal)(_random.NextDouble() * 6 - 3)
                },
                new PortfolioHolding
                {
                    Symbol = "ADA",
                    Name = "Cardano", 
                    Amount = 1500m + (decimal)(_random.NextDouble() * 100 - 50),
                    Price = _basePrices["ADAUSDT"] * (1 + (decimal)(_random.NextDouble() * 0.02 - 0.01)),
                    Change24h = (decimal)(_random.NextDouble() * 8 - 4)
                }
            };

            // Calculate USD values and allocations
            foreach (var holding in holdings)
            {
                holding.UsdValue = holding.Amount * holding.Price;
            }

            var totalValue = holdings.Sum(h => h.UsdValue);
            foreach (var holding in holdings)
            {
                holding.AllocationPercentage = totalValue > 0 ? (holding.UsdValue / totalValue) * 100 : 0;
            }

            return holdings;
        }

        public async Task<List<TradeData>> GetRecentTradesAsync()
        {
            await Task.Delay(50);
            
            var trades = new List<TradeData>();
            var symbols = _basePrices.Keys.ToArray();
            
            for (int i = 0; i < 10; i++)
            {
                var symbol = symbols[_random.Next(symbols.Length)];
                var isBuy = _random.NextDouble() > 0.5;
                var basePrice = _basePrices[symbol];
                var amount = (decimal)(_random.NextDouble() * 0.1);
                
                trades.Add(new TradeData
                {
                    Symbol = symbol,
                    Side = isBuy ? "BUY" : "SELL",
                    Amount = amount,
                    Price = basePrice * (1 + (decimal)(_random.NextDouble() * 0.01 - 0.005)),
                    Timestamp = DateTime.UtcNow.AddMinutes(-_random.Next(1, 1440)) // Last 24 hours
                });
            }
            
            return trades.OrderByDescending(t => t.Timestamp).ToList();
        }

        public async Task<TradingBotStatus> GetBotStatusAsync()
        {
            await Task.Delay(25);
            
            return new TradingBotStatus
            {
                IsRunning = _random.NextDouble() > 0.3, // 70% chance running
                TotalProfit = 1234.56 + (double)(_random.NextDouble() * 200 - 100),
                TotalTrades = 123 + _random.Next(-5, 10),
                LastUpdate = DateTime.Now.AddMinutes(-_random.Next(1, 30)),
                Status = _random.NextDouble() > 0.3 ? "Running" : "Stopped"
            };
        }
    }

    public class PortfolioHolding
    {
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Price { get; set; }
        public decimal UsdValue { get; set; }
        public decimal AllocationPercentage { get; set; }
        public decimal Change24h { get; set; }
    }

    public class TradeData
    {
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Price { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal Total => Amount * Price;
    }
}