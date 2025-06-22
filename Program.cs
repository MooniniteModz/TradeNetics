using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Data;

// --- Configuration ---
public static class TraderConfig
{
    // IMPORTANT: Use secure storage for API credentials in production
    public const string ApiKey = "5gsCN2u0GYfe19atgOudQunSkWiiRfrERQPYXLJIRqrW3N1QAnUKUze0LCoJQkk7";
    public const string ApiSecret = "oWbrPBbDTdMgkkuvWIQkDSutrwohd1PIcAMOlWampkwnVSERKAt5XvrdJuke8cA6";
    public const string BaseApiUrl = "https://api.binance.com";
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
    public string PredictedAction { get; set; } = string.Empty;
    
    [ColumnName("Score")]
    public float[] Confidence { get; set; } = Array.Empty<float>();
}

public class TrainingData : CryptoFeatures
{
    public string Label { get; set; } = string.Empty; // "BUY", "SELL", "HOLD"
}

// --- Enhanced Data Models ---
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

public class Ticker24hr
{
    public string Symbol { get; set; } = string.Empty;
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

// --- Original API Models (Enhanced) ---
public class TickerPrice
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class OrderRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public string TimeInForce { get; set; } = string.Empty;
    public long? Timestamp { get; set; }
}

public class OrderResponse
{
    public string Symbol { get; set; } = string.Empty;
    public long OrderId { get; set; }
    public string ClientOrderId { get; set; } = string.Empty;
    public long TransactTime { get; set; }
    public decimal Price { get; set; }
    public decimal OrigQty { get; set; }
    public decimal ExecutedQty { get; set; }
    public decimal CummulativeQuoteQty { get; set; }
    public string Status { get; set; } = string.Empty;
    public string TimeInForce { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
}

public class AccountBalance
{
    public string Asset { get; set; } = string.Empty;
    public decimal Free { get; set; }
    public decimal Locked { get; set; }
    public decimal Total => Free + Locked;
}

// --- Technical Analysis Helper ---
public static class TechnicalAnalysis
{
    public static float CalculateRSI(List<decimal> prices, int period = 14)
    {
        if (prices.Count < period + 1) return 50f; // Neutral RSI
        
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
        
        // For simplicity, using SMA instead of EMA for signal line
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

// --- ML.NET Trading Model ---
public class MLTradingModel
{
    private readonly MLContext _mlContext;
    private ITransformer? _model;
    
    public MLTradingModel()
    {
        _mlContext = new MLContext(seed: 0);
    }
    
    public void TrainModel(List<TrainingData> trainingData)
    {
        Console.WriteLine("Training ML model...");
        
        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);
        
        // Print schema to debug
        Console.WriteLine("Data schema:");
        foreach (var column in dataView.Schema)
        {
            Console.WriteLine($"Column: {column.Name}, Type: {column.Type}");
        }
        
        // Define the training pipeline with explicit column names
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
        
        // Train the model
        _model = pipeline.Fit(dataView);
        
        // Create prediction engine using TrainingData type since it has the Label column structure
        // But we'll only use it with CryptoFeatures (which doesn't have Label)
        var tempPredictionEngine = _mlContext.Model.CreatePredictionEngine<TrainingData, TradingPrediction>(_model);
        
        // We'll handle the conversion in PredictAction method
        Console.WriteLine("ML model training completed.");
    }
    
    public TradingPrediction PredictAction(CryptoFeatures features)
    {
        if (_model == null)
        {
            throw new InvalidOperationException("Model must be trained before making predictions.");
        }
        
        // Convert CryptoFeatures to TrainingData for prediction
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
            Label = "HOLD" // Dummy value, not used in prediction
        };
        
        // Create a temporary prediction engine for this single prediction
        var predictionEngine = _mlContext.Model.CreatePredictionEngine<TrainingData, TradingPrediction>(_model);
        return predictionEngine.Predict(trainingData);
    }
    
    public void SaveModel(string filePath)
    {
        if (_model == null)
        {
            throw new InvalidOperationException("Model must be trained before saving.");
        }
        
        _mlContext.Model.Save(_model, null, filePath);
        Console.WriteLine($"Model saved to {filePath}");
    }
    
    public void LoadModel(string filePath)
    {
        if (File.Exists(filePath))
        {
            _model = _mlContext.Model.Load(filePath, out var modelInputSchema);
            Console.WriteLine($"Model loaded from {filePath}");
        }
        else
        {
            Console.WriteLine($"Model file not found: {filePath}");
        }
    }
}

// --- Enhanced Crypto Trading Service ---
public class CryptoTraderService
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly string _baseApiUrl;
    private readonly MLTradingModel _mlModel;
    private readonly Dictionary<string, List<KlineData>> _priceHistory;
    
    public CryptoTraderService(string apiKey, string apiSecret, string baseApiUrl)
    {
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _baseApiUrl = baseApiUrl;
        _mlModel = new MLTradingModel();
        _priceHistory = new Dictionary<string, List<KlineData>>();
        
        _httpClient.BaseAddress = new Uri(_baseApiUrl);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", _apiKey);
    }
    
    private string CreateSignature(string queryString)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_apiSecret);
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
            HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var ticker = JsonSerializer.Deserialize<TickerPrice>(responseBody, options);
            return ticker;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error fetching price for {symbol}: {e.Message}");
            return null;
        }
    }
    
    public async Task<Ticker24hr?> Get24hrTickerAsync(string symbol)
    {
        string endpoint = $"/api/v3/ticker/24hr?symbol={symbol.ToUpper()}";
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var ticker = JsonSerializer.Deserialize<Ticker24hr>(responseBody, options);
            return ticker;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error fetching 24hr ticker for {symbol}: {e.Message}");
            return null;
        }
    }
    
    public async Task<List<KlineData>> GetKlineDataAsync(string symbol, string interval = "1h", int limit = 100)
    {
        string endpoint = $"/api/v3/klines?symbol={symbol.ToUpper()}&interval={interval}&limit={limit}";
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            
            var jsonArray = JsonSerializer.Deserialize<decimal[][]>(responseBody);
            if (jsonArray == null) return new List<KlineData>();
            
            var klineData = jsonArray.Select(k => new KlineData
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
            
            // Store price history for technical analysis
            _priceHistory[symbol] = klineData;
            
            return klineData;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error fetching kline data for {symbol}: {e.Message}");
            return new List<KlineData>();
        }
    }
    
    public CryptoFeatures? ExtractFeatures(string symbol, Ticker24hr ticker24hr)
    {
        if (!_priceHistory.ContainsKey(symbol) || _priceHistory[symbol].Count < 26)
        {
            Console.WriteLine($"Insufficient price history for {symbol}");
            return null;
        }
        
        var priceHistory = _priceHistory[symbol];
        var closePrices = priceHistory.Select(k => k.Close).ToList();
        var volumes = priceHistory.Select(k => k.Volume).ToList();
        
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
    
    public void InitializeDemoModel()
    {
        // Create demo training data for illustration
        var trainingData = GenerateDemoTrainingData();
        _mlModel.TrainModel(trainingData);
    }
    
    private List<TrainingData> GenerateDemoTrainingData()
    {
        var random = new Random(42);
        var trainingData = new List<TrainingData>();
        
        // Generate synthetic training data
        for (int i = 0; i < 1000; i++)
        {
            var rsi = random.Next(0, 100);
            var priceChange = (random.NextDouble() - 0.5) * 10; // -5% to +5%
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
    
    public async Task<string> GetMLPrediction(string symbol)
    {
        try
        {
            // Get current market data
            var ticker24hr = await Get24hrTickerAsync(symbol);
            if (ticker24hr == null) return "HOLD";
            
            // Get price history for technical analysis
            await GetKlineDataAsync(symbol);
            
            // Extract features
            var features = ExtractFeatures(symbol, ticker24hr);
            if (features == null) return "HOLD";
            
            // Get ML prediction
            var prediction = _mlModel.PredictAction(features);
            
            Console.WriteLine($"ML Prediction for {symbol}: {prediction.PredictedAction}");
            Console.WriteLine($"Features - RSI: {features.RSI:F2}, Price Change: {features.PriceChange24h:F2}%, Volume Ratio: {features.VolumeRatio:F2}");
            
            return prediction.PredictedAction;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error getting ML prediction for {symbol}: {e.Message}");
            return "HOLD";
        }
    }
    
    public async Task<OrderResponse?> PlaceOrderAsync(OrderRequest orderRequest)
    {
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
            HttpResponseMessage response = await _httpClient.PostAsync($"{endpoint}?{fullQueryString}", requestContent);
            string responseBody = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error placing order: {response.StatusCode} - {responseBody}");
                return null;
            }
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var orderResponse = JsonSerializer.Deserialize<OrderResponse>(responseBody, options);
            return orderResponse;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Request error placing order for {orderRequest.Symbol}: {e.Message}");
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
            HttpResponseMessage response = await _httpClient.GetAsync($"{endpoint}?{fullQueryString}");
            string responseBody = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error fetching account balances: {response.StatusCode} - {responseBody}");
                return null;
            }
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using (JsonDocument doc = JsonDocument.Parse(responseBody))
            {
                JsonElement balancesElement;
                if (doc.RootElement.TryGetProperty("balances", out balancesElement))
                {
                    return JsonSerializer.Deserialize<AccountBalance[]>(balancesElement.GetRawText(), options);
                }
                Console.WriteLine("Could not find 'balances' property in account info response.");
                return null;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Request error fetching account balances: {e.Message}");
            return null;
        }
    }
    
    public void SaveModel(string filePath) => _mlModel.SaveModel(filePath);
    public void LoadModel(string filePath) => _mlModel.LoadModel(filePath);
}

// --- Main Program ---
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting ML.NET Crypto Trading Bot...");
        
        // Initialize trading service
        var traderService = new CryptoTraderService(
            TraderConfig.ApiKey, 
            TraderConfig.ApiSecret, 
            TraderConfig.BaseApiUrl);
        
        // Initialize or load ML model
        string modelPath = "crypto_trading_model.zip";
        if (File.Exists(modelPath))
        {
            traderService.LoadModel(modelPath);
            Console.WriteLine("Loaded existing ML model.");
        }
        else
        {
            Console.WriteLine("Training new ML model with demo data...");
            traderService.InitializeDemoModel();
            traderService.SaveModel(modelPath);
        }
        
        // Trading symbols to monitor
        string[] symbols = { "BTCUSDT", "ETHUSDT", "ADAUSDT" };
        
        foreach (string symbol in symbols)
        {
            Console.WriteLine($"\n--- Analyzing {symbol} ---");
            
            // Get ML prediction
            string prediction = await traderService.GetMLPrediction(symbol);
            
            // Get current price
            var currentPrice = await traderService.GetPriceAsync(symbol);
            if (currentPrice == null) continue;
            
            Console.WriteLine($"Current price of {symbol}: ${currentPrice.Price:F2}");
            Console.WriteLine($"ML Prediction: {prediction}");
            
            // Execute trading logic based on ML prediction
            if (prediction == "BUY")
            {
                Console.WriteLine($"ML model suggests BUY for {symbol}");
                
                // Example: Place a small buy order
                var buyOrder = new OrderRequest
                {
                    Symbol = symbol,
                    Side = "BUY",
                    Type = "MARKET", // Use MARKET for immediate execution in demo
                    Quantity = symbol == "BTCUSDT" ? 0.001m : 0.01m, // Adjust quantity based on symbol
                    TimeInForce = "GTC"
                };
                
                // Uncomment to place actual orders (CAREFUL!)
                // var orderResponse = await traderService.PlaceOrderAsync(buyOrder);
                // if (orderResponse != null)
                // {
                //     Console.WriteLine($"BUY order placed: {orderResponse.OrderId}");
                // }
                
                Console.WriteLine($"Would place BUY order for {buyOrder.Quantity} {symbol}");
            }
            else if (prediction == "SELL")
            {
                Console.WriteLine($"ML model suggests SELL for {symbol}");
                Console.WriteLine($"Would place SELL order for {symbol}");
            }
            else
            {
                Console.WriteLine($"ML model suggests HOLD for {symbol}");
            }
        }
        
        // Show account balances
        Console.WriteLine("\n--- Account Balances ---");
        var balances = await traderService.GetAccountBalancesAsync();
        if (balances != null)
        {
            foreach (var balance in balances.Where(b => b.Free > 0 || b.Locked > 0))
            {
                Console.WriteLine($"{balance.Asset}: Free={balance.Free:F8}, Locked={balance.Locked:F8}");
            }
        }
        
        Console.WriteLine("\nML.NET Crypto Trading Bot completed analysis.");
        Console.WriteLine("IMPORTANT: This is for educational purposes. Use proper risk management in live trading!");
    }
}

// Required NuGet packages:
// - Microsoft.ML
// - System.Text.Json
// - System.Net.Http