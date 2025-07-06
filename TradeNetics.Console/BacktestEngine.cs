using Microsoft.Extensions.Logging;
using TradeNetics.Shared.Data;
using TradeNetics.Shared.Interfaces;
using TradeNetics.Shared.Models;

namespace TradeNetics.Console.Services
{
    public class BacktestEngine
    {
        private readonly IMarketDataRepository _marketDataRepository;
        private readonly IMLTradingModel _mlModel;
        private readonly ILogger<BacktestEngine> _logger;
        private readonly TradingConfiguration _config;

        public BacktestEngine(
            IMarketDataRepository marketDataRepository,
            IMLTradingModel mlModel,
            ILogger<BacktestEngine> logger,
            TradingConfiguration config)
        {
            _marketDataRepository = marketDataRepository;
            _mlModel = mlModel;
            _logger = logger;
            _config = config;
        }

        public async Task<BacktestResults> RunBacktestAsync(DateTime startDate, DateTime endDate, string[] symbols)
        {
            _logger.LogInformation("Running backtest from {StartDate} to {EndDate}", startDate, endDate);

            var results = new BacktestResults
            {
                StartDate = startDate,
                EndDate = endDate,
                Trades = new List<TradeRecord>()
            };

            decimal initialBalance = _config.BacktestInitialBalance;
            decimal currentBalance = initialBalance;
            var positions = new Dictionary<string, decimal>(); // symbol -> quantity

            foreach (var symbol in symbols)
            {
                var marketData = await _marketDataRepository.GetMarketDataAsync(symbol, startDate, endDate);

                foreach (var data in marketData)
                {
                    var features = new CryptoFeatures
                    {
                        Price = (float)data.Close,
                        Volume = (float)data.Volume,
                        PriceChange24h = (float)data.PriceChange24h,
                        VolumeChange24h = (float)data.VolumeChange24h,
                        RSI = data.RSI,
                        MovingAverage5 = data.MovingAverage5,
                        MovingAverage20 = data.MovingAverage20,
                        BollingerUpper = data.BollingerUpper,
                        BollingerLower = data.BollingerLower,
                        MACD = data.MACD,
                        Signal = data.Signal,
                        VolumeRatio = data.VolumeRatio
                    };

                    var prediction = _mlModel.PredictAction(features);
                    var confidence = prediction.Confidence?.Max() ?? 0f;

                    if (confidence > (float)_config.MinConfidenceScore)
                    {
                        var trade = SimulateTrade(symbol, prediction.PredictedAction, data.Close, currentBalance, positions);
                        if (trade != null)
                        {
                            trade.ExecutedAt = data.Timestamp;
                            trade.ConfidenceScore = confidence;
                            results.Trades.Add(trade);

                            // Update balance and positions
                            if (trade.Side == "BUY")
                            {
                                currentBalance -= trade.Quantity * trade.Price;
                                positions[symbol] = positions.GetValueOrDefault(symbol, 0) + trade.Quantity;
                            }
                            else if (trade.Side == "SELL")
                            {
                                currentBalance += trade.Quantity * trade.Price;
                                positions[symbol] = positions.GetValueOrDefault(symbol, 0) - trade.Quantity;
                            }
                        }
                    }
                }
            }

            // Calculate final portfolio value
            decimal finalValue = currentBalance;
            foreach (var position in positions)
            {
                if (position.Value > 0)
                {
                    var latestPrice = GetLatestPrice(position.Key);
                    finalValue += position.Value * latestPrice;
                }
            }

            results.TotalReturn = (finalValue - initialBalance) / initialBalance;
            results.TotalTrades = results.Trades.Count;
            results.WinRate = results.Trades.Count > 0 ?
                results.Trades.Count(t => t.PnL > 0) / (decimal)results.Trades.Count : 0;

            _logger.LogInformation("Backtest completed. Total return: {Return:P2}, Win rate: {WinRate:P2}",
                results.TotalReturn, results.WinRate);

            return results;
        }

        private TradeRecord? SimulateTrade(string symbol, string prediction, decimal price, decimal balance, Dictionary<string, decimal> positions)
        {
            if (prediction == "BUY" && balance > price * 0.01m) // Minimum trade size
            {
                var quantity = Math.Min(balance * 0.1m / price, 0.01m); // 10% of balance or min size
                return new TradeRecord
                {
                    Symbol = symbol,
                    Side = "BUY",
                    Quantity = quantity,
                    Price = price,
                    MLPrediction = prediction,
                    IsPaperTrade = true
                };
            }
            else if (prediction == "SELL" && positions.GetValueOrDefault(symbol, 0) > 0)
            {
                var quantity = Math.Min(positions[symbol], 0.01m);
                return new TradeRecord
                {
                    Symbol = symbol,
                    Side = "SELL",
                    Quantity = quantity,
                    Price = price,
                    MLPrediction = prediction,
                    IsPaperTrade = true
                };
            }

            return null;
        }

        private decimal GetLatestPrice(string symbol)
        {
            // This would normally fetch from API, for simulation return a placeholder
            return 50000m; // Placeholder price
        }
    }
}
