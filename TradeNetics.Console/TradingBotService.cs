using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradeNetics.Shared.Data;
using TradeNetics.Shared.Interfaces;
using TradeNetics.Shared.Models;

namespace TradeNetics.Console.Services
{
    public class TradingBotService : BackgroundService
    {
        private readonly ICryptoTraderService _traderService;
        private readonly IRiskManager _riskManager;
        private readonly IPortfolioManager _portfolioManager;
        private readonly IMLTradingModel _mlModel;
        private readonly TradingConfiguration _config;
        private readonly TradingDbContext _context;
        private readonly ILogger<TradingBotService> _logger;

        public TradingBotService(
            ICryptoTraderService traderService,
            IRiskManager riskManager,
            IPortfolioManager portfolioManager,
            IMLTradingModel mlModel,
            TradingConfiguration config,
            TradingDbContext context,
            ILogger<TradingBotService> logger)
        {
            _traderService = traderService;
            _riskManager = riskManager;
            _portfolioManager = portfolioManager;
            _mlModel = mlModel;
            _config = config;
            _context = context;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Trading bot service started");

            // Initialize or load ML model
            InitializeModel();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunTradingCycleAsync();
                    await _portfolioManager.SavePortfolioSnapshotAsync();

                    // Wait 5 minutes before next cycle
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in trading cycle");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Short delay on error
                }
            }
        }

        private void InitializeModel()
        {
            string modelPath = "crypto_trading_model.zip";

            if (File.Exists(modelPath))
            {
                _mlModel.LoadModel(modelPath);
                _logger.LogInformation("Loaded existing ML model");
            }
            else
            {
                _logger.LogInformation("Training new ML model...");
                var trainingData = GenerateTrainingData();
                _mlModel.TrainModel(trainingData);
                _mlModel.SaveModel(modelPath);
            }
        }

        private List<TrainingData> GenerateTrainingData()
        {
            // In a real implementation, you would load historical data from your database
            // For now, we'll generate synthetic data
            var random = new Random(42);
            var trainingData = new List<TrainingData>();

            for (int i = 0; i < 1000; i++)
            {
                var rsi = random.Next(0, 100);
                var priceChange = (random.NextDouble() - 0.5) * 10;
                var volume = random.Next(1000, 10000);

                string label;
                if (rsi < 30 && priceChange < -2) label = "BUY";
                else if (rsi > 70 && priceChange > 2) label = "SELL";
                else label = "HOLD";

                trainingData.Add(new TrainingData
                {
                    Price = (float)(40000 + random.Next(-5000, 5000)),
                    Volume = volume,
                    PriceChange24h = (float)priceChange,
                    VolumeChange24h = (float)(0.8 + random.NextDouble() * 0.4),
                    RSI = rsi,
                    MovingAverage5 = (float)(40000 + random.Next(-1000, 1000)),
                    MovingAverage20 = (float)(40000 + random.Next(-2000, 2000)),
                    BollingerUpper = (float)(42000 + random.Next(-500, 500)),
                    BollingerLower = (float)(38000 + random.Next(-500, 500)),
                    MACD = (float)(random.NextDouble() - 0.5) * 1000,
                    Signal = (float)(random.NextDouble() - 0.5) * 800,
                    VolumeRatio = (float)(0.5 + random.NextDouble()),
                    Label = label
                });
            }

            return trainingData;
        }

        private async Task RunTradingCycleAsync()
        {
            _logger.LogInformation("Starting trading cycle...");

            var portfolio = await _portfolioManager.GetCurrentPortfolioAsync();

            foreach (string symbol in _config.TradingSymbols)
            {
                try
                {
                    await ProcessSymbolAsync(symbol, portfolio);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing symbol {Symbol}", symbol);
                }
            }
        }

        private async Task ProcessSymbolAsync(string symbol, Portfolio portfolio)
        {
            _logger.LogInformation("Analyzing {Symbol}...", symbol);

            var prediction = await _traderService.GetMLPredictionAsync(symbol);
            var currentPrice = await _traderService.GetPriceAsync(symbol);

            if (currentPrice == null)
            {
                _logger.LogWarning("Could not get price for {Symbol}", symbol);
                return;
            }

            _logger.LogInformation("{Symbol}: Price=${Price:F2}, Prediction={Prediction}",
                symbol, currentPrice.Price, prediction);

            if (prediction == "BUY")
            {
                await ExecuteBuyOrderAsync(symbol, currentPrice.Price, portfolio);
            }
            else if (prediction == "SELL")
            {
                await ExecuteSellOrderAsync(symbol, currentPrice.Price, portfolio);
            }
        }

        private async Task ExecuteBuyOrderAsync(string symbol, decimal price, Portfolio portfolio)
        {
            var quantity = _config.SymbolQuantities.GetValueOrDefault(symbol, 0.001m);

            var orderRequest = new OrderRequest
            {
                Symbol = symbol,
                Side = "BUY",
                Type = "MARKET",
                Quantity = quantity,
                TimeInForce = "GTC"
            };

            if (_riskManager.CanPlaceOrder(orderRequest, portfolio.TotalValue))
            {
                var orderResponse = await _traderService.PlaceOrderAsync(orderRequest);

                if (orderResponse != null)
                {
                    await RecordTradeAsync(orderResponse, "BUY", portfolio.TotalValue);
                    _logger.LogInformation("BUY order executed: {OrderId} for {Quantity} {Symbol}",
                        orderResponse.OrderId, orderResponse.ExecutedQty, symbol);
                }
            }
            else
            {
                _logger.LogWarning("BUY order rejected by risk manager for {Symbol}", symbol);
            }
        }

        private async Task ExecuteSellOrderAsync(string symbol, decimal price, Portfolio portfolio)
        {
            // Check if we have position to sell
            var balance = portfolio.Balances.FirstOrDefault(b => b.Asset == symbol.Replace("USDT", ""));

            if (balance?.Free > 0)
            {
                var quantity = Math.Min(balance.Free, _config.SymbolQuantities.GetValueOrDefault(symbol, 0.001m));

                var orderRequest = new OrderRequest
                {
                    Symbol = symbol,
                    Side = "SELL",
                    Type = "MARKET",
                    Quantity = quantity,
                    TimeInForce = "GTC"
                };

                var orderResponse = await _traderService.PlaceOrderAsync(orderRequest);

                if (orderResponse != null)
                {
                    await RecordTradeAsync(orderResponse, "SELL", portfolio.TotalValue);
                    _logger.LogInformation("SELL order executed: {OrderId} for {Quantity} {Symbol}",
                        orderResponse.OrderId, orderResponse.ExecutedQty, symbol);
                }
            }
            else
            {
                _logger.LogInformation("No position to sell for {Symbol}", symbol);
            }
        }

        private async Task RecordTradeAsync(OrderResponse orderResponse, string side, decimal portfolioValue)
        {
            var trade = new TradeRecord
            {
                Symbol = orderResponse.Symbol,
                Side = side,
                Quantity = orderResponse.ExecutedQty,
                Price = orderResponse.Price,
                ExecutedAt = DateTimeOffset.FromUnixTimeMilliseconds(orderResponse.TransactTime).DateTime,
                OrderId = orderResponse.OrderId.ToString(),
                IsPaperTrade = _config.PaperTradingMode,
                PortfolioValueBefore = portfolioValue,
                MLPrediction = side
            };

            _context.TradeRecords.Add(trade);
            await _context.SaveChangesAsync();
        }
    }
}
