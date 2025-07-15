using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradeNetics.Shared.Models;

namespace TradeNetics.WebApp.Data
{
    public class MockCryptoDataService : ICryptoDataService
    {
        public async Task<List<MarketData>> GetMarketDataAsync()
        {
            await Task.Delay(100); // Simulate API call
            
            var mockData = new List<MarketData>
            {
                new MarketData
                {
                    Id = 1,
                    Symbol = "BTCUSDT",
                    Timestamp = DateTime.UtcNow,
                    Open = 43250.50m,
                    High = 43500.75m,
                    Low = 42800.25m,
                    Close = 43150.00m,
                    Volume = 1250.75m,
                    RSI = 55.2f,
                    MovingAverage5 = 43100.0f,
                    MovingAverage20 = 42900.0f,
                    BollingerUpper = 43600.0f,
                    BollingerLower = 42600.0f,
                    MACD = 125.5f,
                    Signal = 120.0f,
                    VolumeRatio = 1.15f,
                    PriceChange24h = 350.00m,
                    VolumeChange24h = 125.50m
                },
                new MarketData
                {
                    Id = 2,
                    Symbol = "ETHUSDT",
                    Timestamp = DateTime.UtcNow,
                    Open = 2650.25m,
                    High = 2685.50m,
                    Low = 2620.00m,
                    Close = 2670.75m,
                    Volume = 8500.25m,
                    RSI = 62.8f,
                    MovingAverage5 = 2665.0f,
                    MovingAverage20 = 2640.0f,
                    BollingerUpper = 2720.0f,
                    BollingerLower = 2580.0f,
                    MACD = 15.2f,
                    Signal = 12.8f,
                    VolumeRatio = 1.25f,
                    PriceChange24h = 45.75m,
                    VolumeChange24h = 380.00m
                }
            };
            
            return mockData;
        }

        public async Task<List<PortfolioHolding>> GetPortfolioHoldingsAsync()
        {
            await Task.Delay(50);
            
            return new List<PortfolioHolding>
            {
                new PortfolioHolding
                {
                    Id = 1,
                    Symbol = "BTC",
                    Quantity = 0.5m,
                    AveragePrice = 42000.00m,
                    CurrentPrice = 43150.00m,
                    MarketValue = 21575.00m,
                    PnL = 575.00m,
                    PnLPercent = 2.74m,
                    LastUpdated = DateTime.UtcNow,
                    IsStableCoin = false,
                    Change24h = 1.25m,
                    AllocationPercentage = 55.0m,
                    Price = 43150.00m,
                    Amount = 0.5m,
                    UsdValue = 21575.00m,
                    Name = "Bitcoin"
                },
                new PortfolioHolding
                {
                    Id = 2,
                    Symbol = "ETH",
                    Quantity = 5.0m,
                    AveragePrice = 2600.00m,
                    CurrentPrice = 2670.75m,
                    MarketValue = 13353.75m,
                    PnL = 353.75m,
                    PnLPercent = 2.72m,
                    LastUpdated = DateTime.UtcNow,
                    IsStableCoin = false,
                    Change24h = 2.72m,
                    AllocationPercentage = 34.0m,
                    Price = 2670.75m,
                    Amount = 5.0m,
                    UsdValue = 13353.75m,
                    Name = "Ethereum"
                },
                new PortfolioHolding
                {
                    Id = 3,
                    Symbol = "USDT",
                    Quantity = 5000.00m,
                    AveragePrice = 1.00m,
                    CurrentPrice = 1.00m,
                    MarketValue = 5000.00m,
                    PnL = 0.00m,
                    PnLPercent = 0.00m,
                    LastUpdated = DateTime.UtcNow,
                    IsStableCoin = true,
                    Change24h = 0.00m,
                    AllocationPercentage = 11.0m,
                    Price = 1.00m,
                    Amount = 5000.00m,
                    UsdValue = 5000.00m,
                    Name = "Tether USD"
                }
            };
        }

        public async Task<List<TradeData>> GetRecentTradesAsync()
        {
            await Task.Delay(75);
            
            return new List<TradeData>
            {
                new TradeData
                {
                    Id = 1,
                    Symbol = "BTCUSDT",
                    Side = "BUY",
                    Quantity = 0.1m,
                    Price = 43000.00m,
                    ExecutedAt = DateTime.UtcNow.AddMinutes(-15),
                    Timestamp = DateTime.UtcNow.AddMinutes(-15),
                    MLPrediction = "BULLISH",
                    PnL = 15.00m,
                    PortfolioValueBefore = 39000.00m,
                    PortfolioValueAfter = 39015.00m,
                    OrderId = "ORDER_001",
                    IsPaperTrade = false,
                    ConfidenceScore = 0.85f,
                    Status = "FILLED",
                    Fee = 2.15m,
                    Amount = 0.1m,
                    Total = 4300.00m
                },
                new TradeData
                {
                    Id = 2,
                    Symbol = "ETHUSDT",
                    Side = "SELL",
                    Quantity = 1.0m,
                    Price = 2665.00m,
                    ExecutedAt = DateTime.UtcNow.AddMinutes(-45),
                    Timestamp = DateTime.UtcNow.AddMinutes(-45),
                    MLPrediction = "BEARISH",
                    PnL = -25.50m,
                    PortfolioValueBefore = 39040.50m,
                    PortfolioValueAfter = 39015.00m,
                    OrderId = "ORDER_002",
                    IsPaperTrade = false,
                    ConfidenceScore = 0.72f,
                    Status = "FILLED",
                    Fee = 1.33m,
                    Amount = 1.0m,
                    Total = 2665.00m
                }
            };
        }

        public async Task<TradingBotStatus> GetBotStatusAsync()
        {
            await Task.Delay(25);
            
            return new TradingBotStatus
            {
                IsRunning = true,
                LastUpdate = DateTime.UtcNow,
                CurrentStrategy = "ML_MOMENTUM",
                TotalPnL = 1245.75m,
                TotalProfit = 1245.75m,
                DailyPnL = 85.25m,
                TotalTrades = 156,
                DailyTrades = 8,
                PortfolioValue = 39928.75m,
                AvailableBalance = 5000.00m,
                Status = "ACTIVE",
                LastError = "",
                LastTradeTime = DateTime.UtcNow.AddMinutes(-15),
                ModelConfidence = 0.85f,
                ActiveSymbols = "BTCUSDT,ETHUSDT,ADAUSDT",
                PaperTradingMode = false
            };
        }
    }
}