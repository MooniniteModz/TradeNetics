using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using Polly;
using Polly.CircuitBreaker;

// --- Configuration ---
public class TradingConfiguration
{
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string BaseApiUrl { get; set; } = "https://api.binance.com";
    public Dictionary<string, decimal> SymbolQuantities { get; set; } = new();
    public decimal MinConfidenceScore { get; set; } = 0.7m;
    public bool PaperTradingMode { get; set; } = true;
    public TimeSpan ModelRetrainingInterval { get; set; } = TimeSpan.FromDays(7);
    public decimal MaxPositionSize { get; set; } = 0.02m; // 2% of portfolio
    public decimal StopLossPercent { get; set; } = 0.05m; // 5% stop loss
    public decimal MaxDailyLoss { get; set; } = 0.10m; // 10% daily loss limit
    public string[] TradingSymbols { get; set; } = { "BTCUSDT", "ETHUSDT", "ADAUSDT" };
}


// --- Database Models ---
public class MarketData
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public float RSI { get; set; }
    public float MovingAverage5 { get; set; }
    public float MovingAverage20 { get; set; }
    public float BollingerUpper { get; set; }
    public float BollingerLower { get; set; }
    public float MACD { get; set; }
    public float Signal { get; set; }
    public float VolumeRatio { get; set; }
    public decimal PriceChange24h { get; set; }
    public decimal VolumeChange24h { get; set; }
}

public class TradeRecord
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public string Side { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime ExecutedAt { get; set; }
    public string MLPrediction { get; set; } = "";
    public decimal PnL { get; set; }
    public decimal PortfolioValueBefore { get; set; }
    public decimal PortfolioValueAfter { get; set; }
    public string OrderId { get; set; } = "";
    public bool IsPaperTrade { get; set; }
    public float ConfidenceScore { get; set; }
}

public class ModelPerformance
{
    public int Id { get; set; }
    public DateTime TrainingDate { get; set; }
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public int TrainingDataCount { get; set; }
    public string ModelVersion { get; set; } = "";
}

public class PortfolioSnapshot
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal TotalValue { get; set; }
    public decimal DailyPnL { get; set; }
    public decimal TotalPnL { get; set; }
    public string AssetAllocation { get; set; } = ""; // JSON string
    public decimal RiskScore { get; set; }
}


// --- Database Context ---
public class TradingContext : DbContext
{
    public TradingContext(DbContextOptions<TradingContext> options) : base(options) { }

    public DbSet<MarketData> MarketData { get; set; }
    public DbSet<TradeRecord> TradeRecords { get; set; }
    public DbSet<ModelPerformance> ModelPerformances { get; set; }
    public DbSet<PortfolioSnapshot> PortfolioSnapshots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MarketData>()
            .HasIndex(m => new { m.Symbol, m.Timestamp })
            .IsUnique();

        modelBuilder.Entity<TradeRecord>()
            .HasIndex(t => t.ExecutedAt);

        modelBuilder.Entity<PortfolioSnapshot>()
            .HasIndex(p => p.Timestamp);

        // Configure decimal precision for financial data
        modelBuilder.Entity<MarketData>()
            .Property(m => m.Close)
            .HasPrecision(18, 8);

        modelBuilder.Entity<TradeRecord>()
            .Property(t => t.Price)
            .HasPrecision(18, 8);
    }
}


// --- ML.NET Data Models ---
public class CryptoFeatures
{
    public float Price { get; set; }
    public float Volume { get; set; }
    public float PriceChange24h { get; set; }
    public float VolumeChange24h { get; set; }
    public float RSI { get; set; }
    public float MovingAverage5 { get; set; }
    public float MovingAverage20 { get; set; }
    public float BollingerUpper { get; set; }
    public float BollingerLower { get; set; }
    public float MACD { get; set; }
    public float Signal { get; set; }
    public float VolumeRatio { get; set; }
}

public class TradingPrediction
{
    [ColumnName("PredictedLabel")]
    public string PredictedAction { get; set; } = "";
    
    [ColumnName("Score")]
    public float[] Confidence { get; set; } = Array.Empty<float>();
}

public class TrainingData : CryptoFeatures
{
    public string Label { get; set; } = ""; // "BUY", "SELL", "HOLD"
}


// --- API Models ---
public class TickerPrice
{
    public string Symbol { get; set; } = "";
    public decimal Price { get; set; }
}

public class Ticker24hr
{
    public string Symbol { get; set; } = "";
    public decimal PriceChange { get; set; }
    public decimal PriceChangePercent { get; set; }
    public decimal WeightedAvgPrice { get; set; }
    public decimal PrevClosePrice { get; set; }
    public decimal LastPrice { get; set; }
    public decimal LastQty { get; set; }
    public decimal BidPrice { get; set; }
    public decimal AskPrice { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }
    public decimal Volume { get; set; }
    public decimal QuoteVolume { get; set; }
    public long OpenTime { get; set; }
    public long CloseTime { get; set; }
    public long FirstId { get; set; }
    public long LastId { get; set; }
    public int Count { get; set; }
}

public class KlineData
{
    public long OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public long CloseTime { get; set; }
    public decimal QuoteAssetVolume { get; set; }
    public int NumberOfTrades { get; set; }
    public decimal TakerBuyBaseAssetVolume { get; set; }
    public decimal TakerBuyQuoteAssetVolume { get; set; }
}

public class OrderRequest
{
    public string Symbol { get; set; } = "";
    public string Side { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public string TimeInForce { get; set; } = "";
    public long? Timestamp { get; set; }
}

public class OrderResponse
{
    public string Symbol { get; set; } = "";
    public long OrderId { get; set; }
    public string ClientOrderId { get; set; } = "";
    public long TransactTime { get; set; }
    public decimal Price { get; set; }
    public decimal OrigQty { get; set; }
    public decimal ExecutedQty { get; set; }
    public decimal CummulativeQuoteQty { get; set; }
    public string Status { get; set; } = "";
    public string TimeInForce { get; set; } = "";
    public string Type { get; set; } = "";
    public string Side { get; set; } = "";
}

public class AccountBalance
{
    public string Asset { get; set; } = "";
    public decimal Free { get; set; }
    public decimal Locked { get; set; }
    public decimal Total => Free + Locked;
}

public class Portfolio
{
    public decimal TotalValue { get; set; }
    public decimal DailyPnL { get; set; }
    public decimal TotalPnL { get; set; }
    public Dictionary<string, decimal> AssetAllocation { get; set; } = new();
    public decimal RiskScore { get; set; }
    public List<AccountBalance> Balances { get; set; } = new();
}

public class BacktestResults
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal MaxDrawdown { get; set; }
    public int TotalTrades { get; set; }
    public decimal WinRate { get; set; }
    public List<TradeRecord> Trades { get; set; } = new();
}


// --- Interfaces ---
public interface IMarketDataRepository
{
    Task SaveMarketDataAsync(gg marketData);
    Task<List<MarketData>> GetMarketDataAsync(string symbol, DateTime from, DateTime to);
    Task<List<TrainingData>> GetTrainingDataAsync();
}

public interface ICryptoTraderService
{
    Task<TickerPrice?> GetPriceAsync(string symbol);
    Task<Ticker24hr?> Get24hrTickerAsync(string symbol);
    Task<List<KlineData>> GetKlineDataAsync(string symbol, string interval = "1h", int limit = 100);
    Task<OrderResponse?> PlaceOrderAsync(OrderRequest orderRequest);
    Task<AccountBalance[]?> GetAccountBalancesAsync();
    Task<string> GetMLPredictionAsync(string symbol);
}

public interface IRiskManager
{
    bool CanPlaceOrder(OrderRequest order, decimal portfolioValue);
    decimal CalculatePositionSize(string symbol, decimal confidence, decimal portfolioValue);
    bool IsStopLossTriggered(string symbol, decimal currentPrice, decimal entryPrice, string side);
}

public interface IPortfolioManager
{
    Task<Portfolio> GetCurrentPortfolioAsync();
    Task SavePortfolioSnapshotAsync();
    Task<decimal> CalculatePnLAsync();
}

public interface IMLTradingModel
{
    Task TrainModelAsync(List<TrainingData> trainingData);
    TradingPrediction PredictAction(CryptoFeatures features);
    Task SaveModelAsync(string filePath);
    Task LoadModelAsync(string filePath);
}


// --- Technical Analysis Helper ---
public static class TechnicalAnalysis
{
    public static float CalculateRSI(List<decimal> prices, int period = 14)
    {
        if (prices.Count < period + 1) return 50f;

        var gains = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < prices.Count; i++)
        {
            var change = prices[i] - prices[i - 1];
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? Math.Abs(change) : 0);
        }

        var avgGain = gains.TakeLast(period).Average();
        var avgLoss = losses.TakeLast(period).Average();

        if (avgLoss == 0) return 100f;

        var rs = avgGain / avgLoss;
        return (float)(100 - (100 / (1 + rs)));
    }

    public static float CalculateMovingAverage(List<decimal> prices, int period)
    {
        if (prices.Count < period) return (float)prices.Average();
        return (float)prices.TakeLast(period).Average();
    }

    public static (float upper, float lower) CalculateBollingerBands(List<decimal> prices, int period = 20, float multiplier = 2f)
    {
        if (prices.Count < period)
        {
            var avg = (float)prices.Average();
            return (avg, avg);
        }

        var recentPrices = prices.TakeLast(period).ToList();
        var sma = (float)recentPrices.Average();
        var variance = recentPrices.Select(p => Math.Pow((double)(p - (decimal)sma), 2)).Average();
        var stdDev = (float)Math.Sqrt(variance);

        return (sma + (multiplier * stdDev), sma - (multiplier * stdDev));
    }

    public static (float macd, float signal) CalculateMACD(List<decimal> prices, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        if (prices.Count < slowPeriod) return (0f, 0f);

        var fastEMA = CalculateEMA(prices, fastPeriod);
        var slowEMA = CalculateEMA(prices, slowPeriod);
        var macd = fastEMA - slowEMA;

        var macdLine = new List<decimal> { (decimal)macd };
        var signal = CalculateMovingAverage(macdLine, Math.Min(signalPeriod, macdLine.Count));

        return (macd, signal);
    }

    private static float CalculateEMA(List<decimal> prices, int period)
    {
        if (prices.Count < period) return (float)prices.Average();

        var multiplier = 2.0f / (period + 1);
        var ema = (float)prices.Take(period).Average();

        for (int i = period; i < prices.Count; i++)
        {
            ema = ((float)prices[i] * multiplier) + (ema * (1 - multiplier));
        }

        return ema;
    }
}


// --- Repository Implementation ---
public class MarketDataRepository : IMarketDataRepository
{
    private readonly TradingContext _context;
    private readonly ILogger<MarketDataRepository> _logger;

    public MarketDataRepository(TradingContext context, ILogger<MarketDataRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SaveMarketDataAsync(MarketData marketData)
    {
        try
        {
            var existing = await _context.MarketData
                .FirstOrDefaultAsync(m => m.Symbol == marketData.Symbol && m.Timestamp == marketData.Timestamp);

            if (existing == null)
            {
                _context.MarketData.Add(marketData);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Update existing record
                existing.Close = marketData.Close;
                existing.Volume = marketData.Volume;
                existing.RSI = marketData.RSI;
                existing.MACD = marketData.MACD;
                // Update other fields...
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving market data for {Symbol}", marketData.Symbol);
            throw;
        }
    }

    public async Task<List<MarketData>> GetMarketDataAsync(string symbol, DateTime from, DateTime to)
    {
        return await _context.MarketData
            .Where(m => m.Symbol == symbol && m.Timestamp >= from && m.Timestamp <= to)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }

    public async Task<List<TrainingData>> GetTrainingDataAsync()
    {
        var marketData = await _context.MarketData
            .Where(m => m.Timestamp >= DateTime.UtcNow.AddDays(-30))
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return marketData.Select(m => new TrainingData
        {
            Price = (float)m.Close,
            Volume = (float)m.Volume,
            PriceChange24h = (float)m.PriceChange24h,
            VolumeChange24h = (float)m.VolumeChange24h,
            RSI = m.RSI,
            MovingAverage5 = m.MovingAverage5,
            MovingAverage20 = m.MovingAverage20,
            BollingerUpper = m.BollingerUpper,
            BollingerLower = m.BollingerLower,
            MACD = m.MACD,
            Signal = m.Signal,
            VolumeRatio = m.VolumeRatio,
            Label = DetermineLabel(m) // Generate labels based on future price movement
        }).ToList();
    }

    private string DetermineLabel(MarketData data)
    {
        // Simple labeling logic - you can improve this
        if (data.RSI < 30 && data.PriceChange24h < -2) return "BUY";
        if (data.RSI > 70 && data.PriceChange24h > 2) return "SELL";
        return "HOLD";
    }
}

// --- ML Trading Model Implementation ---
public class MLTradingModel : IMLTradingModel
{
    private readonly MLContext _mlContext;
    private readonly ILogger<MLTradingModel> _logger;
    private ITransformer? _model;
    
    public MLTradingModel(ILogger<MLTradingModel> logger)
    {
        _mlContext = new MLContext(seed: 0);
        _logger = logger;
    }
    
    public async Task TrainModelAsync(List<TrainingData> trainingData)
    {
        try
        {
            _logger.LogInformation("Training ML model with {Count} samples", trainingData.Count);
            
            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);
            
            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "LabelKey", inputColumnName: "Label")
                .Append(_mlContext.Transforms.Concatenate("Features", 
                    nameof(CryptoFeatures.Price),
                    nameof(CryptoFeatures.Volume),
                    nameof(CryptoFeatures.PriceChange24h),
                    nameof(CryptoFeatures.VolumeChange24h),
                    nameof(CryptoFeatures.RSI),
                    nameof(CryptoFeatures.MovingAverage5),
                    nameof(CryptoFeatures.MovingAverage20),
                    nameof(CryptoFeatures.BollingerUpper),
                    nameof(CryptoFeatures.BollingerLower),
                    nameof(CryptoFeatures.MACD),
                    nameof(CryptoFeatures.Signal),
                    nameof(CryptoFeatures.VolumeRatio)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: "LabelKey", featureColumnName: "Features"))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", inputColumnName: "PredictedLabel"));
            
            _model = pipeline.Fit(dataView);
            
            _logger.LogInformation("ML model training completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error training ML model");
            throw;
        }
    }
    
    public TradingPrediction PredictAction(CryptoFeatures features)
    {
        if (_model == null)
            throw new InvalidOperationException("Model must be trained before making predictions");
        
        var trainingData = new TrainingData
        {
            Price = features.Price,
            Volume = features.Volume,
            PriceChange24h = features.PriceChange24h,
            VolumeChange24h = features.VolumeChange24h,
            RSI = features.RSI,
            MovingAverage5 = features.MovingAverage5,
            MovingAverage20 = features.MovingAverage20,
            BollingerUpper = features.BollingerUpper,
            BollingerLower = features.BollingerLower,
            MACD = features.MACD,
            Signal = features.Signal,
            VolumeRatio = features.VolumeRatio,
            Label = "HOLD"
        };
        
        var predictionEngine = _mlContext.Model.CreatePredictionEngine<TrainingData, TradingPrediction>(_model);
        return predictionEngine.Predict(trainingData);
    }
    
    public async Task SaveModelAsync(string filePath)
    {
        if (_model == null)
            throw new InvalidOperationException("Model must be trained before saving");
        
        _mlContext.Model.Save(_model, null, filePath);
        _logger.LogInformation("Model saved to {FilePath}", filePath);
    }
    
    public async Task LoadModelAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            _model = _mlContext.Model.Load(filePath, out var modelInputSchema);
            _logger.LogInformation("Model loaded from {FilePath}", filePath);
        }
        else
        {
            _logger.LogWarning("Model file not found: {FilePath}", filePath);
        }
    }
}


// --- Risk Manager Implementation ---
public class RiskManager : IRiskManager
{
    private readonly TradingConfiguration _config;
    private readonly ILogger<RiskManager> _logger;

    public RiskManager(TradingConfiguration config, ILogger<RiskManager> logger)
    {
        _config = config;
        _logger = logger;
    }

    public bool CanPlaceOrder(OrderRequest order, decimal portfolioValue)
    {
        var positionValue = order.Quantity * (order.Price ?? 0);
        var positionSizePercent = positionValue / portfolioValue;

        if (positionSizePercent > _config.MaxPositionSize)
        {
            _logger.LogWarning("Order rejected: Position size {Size}% exceeds maximum {Max}%",
                positionSizePercent * 100, _config.MaxPositionSize * 100);
            return false;
        }

        return true;
    }

    public decimal CalculatePositionSize(string symbol, decimal confidence, decimal portfolioValue)
    {
        // Kelly Criterion-based position sizing
        var baseSize = _config.MaxPositionSize * portfolioValue;
        var adjustedSize = baseSize * confidence;

        return Math.Min(adjustedSize, _config.MaxPositionSize * portfolioValue);
    }

    public bool IsStopLossTriggered(string symbol, decimal currentPrice, decimal entryPrice, string side)
    {
        if (side.ToUpper() == "BUY")
        {
            var lossPercent = (entryPrice - currentPrice) / entryPrice;
            return lossPercent >= _config.StopLossPercent;
        }
        else if (side.ToUpper() == "SELL")
        {
            var lossPercent = (currentPrice - entryPrice) / entryPrice;
            return lossPercent >= _config.StopLossPercent;
        }

        return false;
    }
}


// --- Portfolio Manager Implementation ---
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


// --- Enhanced Crypto Trading Service ---
public class CryptoTraderService : ICryptoTraderService
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private readonly TradingConfiguration _config;
    private readonly IMLTradingModel _mlModel;
    private readonly IMarketDataRepository _marketDataRepository;
    private readonly ILogger<CryptoTraderService> _logger;
    private readonly IAsyncPolicy _retryPolicy;

    public CryptoTraderService(
        TradingConfiguration config,
        IMLTradingModel mlModel,
        IMarketDataRepository marketDataRepository,
        ILogger<CryptoTraderService> logger)
    {
        _config = config;
        _mlModel = mlModel;
        _marketDataRepository = marketDataRepository;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_config.BaseApiUrl);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", _config.ApiKey);

        // Create retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} after {Delay}ms", retryCount, timespan.TotalMilliseconds);
                });
    }

    private string CreateSignature(string queryString)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_config.ApiSecret);
        using (var hmac = new HMACSHA256(keyBytes))
        {
            var queryBytes = Encoding.UTF8.GetBytes(queryString);
            var hashBytes = hmac.ComputeHash(queryBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }

    public async Task<TickerPrice?> GetPriceAsync(string symbol)
    {
        string endpoint = $"/api/v3/ticker/price?symbol={symbol.ToUpper()}";
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<TickerPrice>(responseBody, options);
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error fetching price for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<Ticker24hr?> Get24hrTickerAsync(string symbol)
    {
        string endpoint = $"/api/v3/ticker/24hr?symbol={symbol.ToUpper()}";
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<Ticker24hr>(responseBody, options);
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error fetching 24hr ticker for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<List<KlineData>> GetKlineDataAsync(string symbol, string interval = "1h", int limit = 100)
    {
        string endpoint = $"/api/v3/klines?symbol={symbol.ToUpper()}&interval={interval}&limit={limit}";
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                var jsonArray = JsonSerializer.Deserialize<decimal[][]>(responseBody);
                if (jsonArray == null) return new List<KlineData>();

                return jsonArray.Select(k => new KlineData
                {
                    OpenTime = (long)k[0],
                    Open = k[1],
                    High = k[2],
                    Low = k[3],
                    Close = k[4],
                    Volume = k[5],
                    CloseTime = (long)k[6],
                    QuoteAssetVolume = k[7],
                    NumberOfTrades = (int)k[8],
                    TakerBuyBaseAssetVolume = k[9],
                    TakerBuyQuoteAssetVolume = k[10]
                }).ToList();
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error fetching kline data for {Symbol}", symbol);
            return new List<KlineData>();
        }
    }

    public async Task<string> GetMLPredictionAsync(string symbol)
    {
        try
        {
            var ticker24hr = await Get24hrTickerAsync(symbol);
            if (ticker24hr == null) return "HOLD";

            var klineData = await GetKlineDataAsync(symbol);
            if (klineData.Count < 26) return "HOLD";

            var features = ExtractFeatures(symbol, ticker24hr, klineData);
            if (features == null) return "HOLD";

            // Save market data to database
            await SaveMarketDataAsync(symbol, ticker24hr, klineData, features);

            var prediction = _mlModel.PredictAction(features);

            _logger.LogInformation("ML Prediction for {Symbol}: {Prediction}", symbol, prediction.PredictedAction);
            _logger.LogInformation("Features - RSI: {RSI:F2}, Price Change: {PriceChange:F2}%, Volume Ratio: {VolumeRatio:F2}",
                features.RSI, features.PriceChange24h, features.VolumeRatio);

            return prediction.PredictedAction;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting ML prediction for {Symbol}", symbol);
            return "HOLD";
        }
    }

    private async Task SaveMarketDataAsync(string symbol, Ticker24hr ticker, List<KlineData> klineData, CryptoFeatures features)
    {
        var latest = klineData.Last();
        var marketData = new MarketData
        {
            Symbol = symbol,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(latest.CloseTime).DateTime,
            Open = latest.Open,
            High = latest.High,
            Low = latest.Low,
            Close = latest.Close,
            Volume = latest.Volume,
            RSI = features.RSI,
            MovingAverage5 = features.MovingAverage5,
            MovingAverage20 = features.MovingAverage20,
            BollingerUpper = features.BollingerUpper,
            BollingerLower = features.BollingerLower,
            MACD = features.MACD,
            Signal = features.Signal,
            VolumeRatio = features.VolumeRatio,
            PriceChange24h = ticker.PriceChangePercent,
            VolumeChange24h = features.VolumeChange24h
        };

        await _marketDataRepository.SaveMarketDataAsync(marketData);
    }

    private CryptoFeatures? ExtractFeatures(string symbol, Ticker24hr ticker24hr, List<KlineData> klineData)
    {
        if (klineData.Count < 26)
        {
            _logger.LogWarning("Insufficient price history for {Symbol}", symbol);
            return null;
        }

        var closePrices = klineData.Select(k => k.Close).ToList();
        var volumes = klineData.Select(k => k.Volume).ToList();

        var rsi = TechnicalAnalysis.CalculateRSI(closePrices);
        var ma5 = TechnicalAnalysis.CalculateMovingAverage(closePrices, 5);
        var ma20 = TechnicalAnalysis.CalculateMovingAverage(closePrices, 20);
        var (bollUpper, bollLower) = TechnicalAnalysis.CalculateBollingerBands(closePrices);
        var (macd, signal) = TechnicalAnalysis.CalculateMACD(closePrices);

        var volumeRatio = volumes.Count >= 2 ?
            (float)(volumes.Last() / volumes[^2]) : 1f;

        return new CryptoFeatures
        {
            Price = (float)ticker24hr.LastPrice,
            Volume = (float)ticker24hr.Volume,
            PriceChange24h = (float)ticker24hr.PriceChangePercent,
            VolumeChange24h = volumeRatio,
            RSI = rsi,
            MovingAverage5 = ma5,
            MovingAverage20 = ma20,
            BollingerUpper = bollUpper,
            BollingerLower = bollLower,
            MACD = macd,
            Signal = signal,
            VolumeRatio = volumeRatio
        };
    }

    public async Task<OrderResponse?> PlaceOrderAsync(OrderRequest orderRequest)
    {
        if (_config.PaperTradingMode)
        {
            _logger.LogInformation("PAPER TRADE: Would place {Side} order for {Quantity} {Symbol}",
                orderRequest.Side, orderRequest.Quantity, orderRequest.Symbol);

            // Simulate order response for paper trading
            return new OrderResponse
            {
                Symbol = orderRequest.Symbol,
                OrderId = DateTime.UtcNow.Ticks,
                ClientOrderId = Guid.NewGuid().ToString(),
                TransactTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Price = orderRequest.Price ?? 0,
                OrigQty = orderRequest.Quantity,
                ExecutedQty = orderRequest.Quantity,
                Status = "FILLED",
                Side = orderRequest.Side,
                Type = orderRequest.Type
            };
        }

        string endpoint = "/api/v3/order";
        orderRequest.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var queryParams = new StringBuilder();
        queryParams.Append($"symbol={orderRequest.Symbol.ToUpper()}");
        queryParams.Append($"&side={orderRequest.Side.ToUpper()}");
        queryParams.Append($"&type={orderRequest.Type.ToUpper()}");

        if (orderRequest.Type == "LIMIT")
        {
            queryParams.Append($"&timeInForce={orderRequest.TimeInForce.ToUpper()}");
            queryParams.Append($"&quantity={orderRequest.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            queryParams.Append($"&price={orderRequest.Price?.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
        else if (orderRequest.Type == "MARKET")
        {
            queryParams.Append($"&quantity={orderRequest.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        queryParams.Append($"&timestamp={orderRequest.Timestamp}");

        string signature = CreateSignature(queryParams.ToString());
        string fullQueryString = $"{queryParams.ToString()}&signature={signature}";

        var requestContent = new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded");

        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                HttpResponseMessage response = await _httpClient.PostAsync($"{endpoint}?{fullQueryString}", requestContent);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error placing order: {StatusCode} - {Response}", response.StatusCode, responseBody);
                    return null;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<OrderResponse>(responseBody, options);
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Request error placing order for {Symbol}", orderRequest.Symbol);
            return null;
        }
    }

    public async Task<AccountBalance[]?> GetAccountBalancesAsync()
    {
        string endpoint = "/api/v3/account";
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string queryString = $"timestamp={timestamp}";

        string signature = CreateSignature(queryString);
        string fullQueryString = $"{queryString}&signature={signature}";

        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                HttpResponseMessage response = await _httpClient.GetAsync($"{endpoint}?{fullQueryString}");
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error fetching account balances: {StatusCode} - {Response}", response.StatusCode, responseBody);
                    return null;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                using (JsonDocument doc = JsonDocument.Parse(responseBody))
                {
                    if (doc.RootElement.TryGetProperty("balances", out JsonElement balancesElement))
                    {
                        return JsonSerializer.Deserialize<AccountBalance[]>(balancesElement.GetRawText(), options);
                    }
                    _logger.LogWarning("Could not find 'balances' property in account info response");
                    return null;
                }
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Request error fetching account balances");
            return null;
        }
    }
}


// --- Backtesting Engine ---
public class BacktestEngine
{
    private readonly IMarketDataRepository _marketDataRepository;
    private readonly IMLTradingModel _mlModel;
    private readonly ILogger<BacktestEngine> _logger;

    public BacktestEngine(
        IMarketDataRepository marketDataRepository,
        IMLTradingModel mlModel,
        ILogger<BacktestEngine> logger)
    {
        _marketDataRepository = marketDataRepository;
        _mlModel = mlModel;
        _logger = logger;
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

        decimal initialBalance = 10000m; // $10,000 starting balance
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

                if (confidence > 0.7f) // Only trade with high confidence
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
                var latestPrice = await GetLatestPrice(position.Key);
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

    private async Task<decimal> GetLatestPrice(string symbol)
    {
        // This would normally fetch from API, for simulation return a placeholder
        return 50000m; // Placeholder price
    }
}


// --- Trading Bot Service ---
public class TradingBotService : BackgroundService
{
    private readonly ICryptoTraderService _traderService;
    private readonly IRiskManager _riskManager;
    private readonly IPortfolioManager _portfolioManager;
    private readonly IMLTradingModel _mlModel;
    private readonly TradingConfiguration _config;
    private readonly TradingContext _context;
    private readonly ILogger<TradingBotService> _logger;

    public TradingBotService(
        ICryptoTraderService traderService,
        IRiskManager riskManager,
        IPortfolioManager portfolioManager,
        IMLTradingModel mlModel,
        TradingConfiguration config,
        TradingContext context,
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
        await InitializeModelAsync();

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

    private async Task InitializeModelAsync()
    {
        string modelPath = "crypto_trading_model.zip";

        if (File.Exists(modelPath))
        {
            await _mlModel.LoadModelAsync(modelPath);
            _logger.LogInformation("Loaded existing ML model");
        }
        else
        {
            _logger.LogInformation("Training new ML model...");
            var trainingData = await GenerateTrainingDataAsync();
            await _mlModel.TrainModelAsync(trainingData);
            await _mlModel.SaveModelAsync(modelPath);
        }
    }

    private async Task<List<TrainingData>> GenerateTrainingDataAsync()
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


// --- Entry Point ---
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
                tradingConfig.ApiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? tradingConfig.ApiKey;
                tradingConfig.ApiSecret = Environment.GetEnvironmentVariable("BINANCE_API_SECRET") ?? tradingConfig.ApiSecret;

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

// Required NuGet packages:
// - Microsoft.EntityFrameworkCore
// - Npgsql.EntityFrameworkCore.PostgreSQL
// - Microsoft.ML
// - Microsoft.Extensions.Hosting
// - Microsoft.Extensions.Logging
// - Polly
// - Serilog.Extensions.Logging.File
// - System.Text.Json
