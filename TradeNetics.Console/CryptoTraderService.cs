using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using TradeNetics.Shared.Data;
using TradeNetics.Shared.Helpers;
using TradeNetics.Shared.Interfaces;
using TradeNetics.Shared.Models;

namespace TradeNetics.Console.Services
{
    public class CryptoTraderService : ICryptoTraderService, IDisposable
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly TradingConfiguration _localConfig;
        private readonly IConfigurationService _configurationService;
        private readonly IMLTradingModel _mlModel;
        private readonly IMarketDataRepository _marketDataRepository;
        private readonly ILogger<CryptoTraderService> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private TradingConfiguration? _sharedConfig;

        public CryptoTraderService(
            TradingConfiguration localConfig,
            IConfigurationService configurationService,
            IMLTradingModel mlModel,
            IMarketDataRepository marketDataRepository,
            ILogger<CryptoTraderService> logger)
        {
            _localConfig = localConfig ?? throw new ArgumentNullException(nameof(localConfig));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _mlModel = mlModel ?? throw new ArgumentNullException(nameof(mlModel));
            _marketDataRepository = marketDataRepository ?? throw new ArgumentNullException(nameof(marketDataRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _retryPolicy = CreateRetryPolicy();
        }

        private async Task<TradingConfiguration> GetEffectiveConfigAsync()
        {
            if (_sharedConfig == null)
            {
                try
                {
                    _sharedConfig = await _configurationService.GetConfiguration();
                    
                    // If shared config has API keys, use them; otherwise use fallback to environment variables
                    if (string.IsNullOrEmpty(_sharedConfig.ApiKey) || string.IsNullOrEmpty(_sharedConfig.ApiSecret))
                    {
                        _sharedConfig.ApiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? "test-api-key";
                        _sharedConfig.ApiSecret = Environment.GetEnvironmentVariable("BINANCE_API_SECRET") ?? "test-api-secret";
                        
                        if (_sharedConfig.ApiKey == "test-api-key" || _sharedConfig.ApiSecret == "test-api-secret")
                        {
                            _logger.LogWarning("Using test API keys. Configure API keys in the WebApp or set BINANCE_API_KEY and BINANCE_API_SECRET environment variables.");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Using API keys from shared configuration");
                    }
                    
                    // Merge with local trading configuration
                    _sharedConfig.MaxPositionSize = _localConfig.MaxPositionSize;
                    _sharedConfig.MaxDailyLoss = _localConfig.MaxDailyLoss;
                    _sharedConfig.TradingEnabled = _localConfig.TradingEnabled;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load shared configuration, using local config with environment variables");
                    _sharedConfig = _localConfig;
                    _sharedConfig.ApiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? "test-api-key";
                    _sharedConfig.ApiSecret = Environment.GetEnvironmentVariable("BINANCE_API_SECRET") ?? "test-api-secret";
                }
            }
            return _sharedConfig;
        }

        private async Task ConfigureHttpClientAsync()
        {
            var config = await GetEffectiveConfigAsync();
            
            // Only configure if not already configured or if configuration has changed
            if (_httpClient.BaseAddress == null || _httpClient.BaseAddress.ToString() != config.BaseApiUrl)
            {
                if (_httpClient.BaseAddress == null)
                {
                    _httpClient.BaseAddress = new Uri(config.BaseApiUrl);
                    _httpClient.DefaultRequestHeaders.Accept.Clear();
                    _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    _httpClient.Timeout = TimeSpan.FromSeconds(30);
                }
            }
            
            // Update API key header (this can be changed after initial configuration)
            _httpClient.DefaultRequestHeaders.Remove("X-MBX-APIKEY");
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", config.ApiKey);
            }
        }

        private AsyncRetryPolicy CreateRetryPolicy()
        {
            return Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>() // Handle timeout exceptions
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry {RetryCount} after {Delay}ms for {Exception}", 
                            retryCount, timespan.TotalMilliseconds, outcome.InnerException?.Message);
                    });
        }

        private async Task<string> CreateSignatureAsync(string queryString)
        {
            var config = await GetEffectiveConfigAsync();
            if (string.IsNullOrEmpty(config.ApiSecret))
                throw new InvalidOperationException("API Secret is not configured");

            var keyBytes = Encoding.UTF8.GetBytes(config.ApiSecret);
            using (var hmac = new HMACSHA256(keyBytes))
            {
                var queryBytes = Encoding.UTF8.GetBytes(queryString);
                var hashBytes = hmac.ComputeHash(queryBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private string CreateSignature(string queryString)
        {
            return CreateSignatureAsync(queryString).GetAwaiter().GetResult();
        }

        public async Task<TickerPrice?> GetPriceAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));

            string endpoint = $"/api/v3/ticker/price?symbol={symbol.ToUpper()}";
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var response = await _httpClient.GetAsync(endpoint);
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
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));

            string endpoint = $"/api/v3/ticker/24hr?symbol={symbol.ToUpper()}";
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var response = await _httpClient.GetAsync(endpoint);
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
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));

            if (limit <= 0 || limit > 1000)
                throw new ArgumentException("Limit must be between 1 and 1000", nameof(limit));

            string endpoint = $"/api/v3/klines?symbol={symbol.ToUpper()}&interval={interval}&limit={limit}";
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var response = await _httpClient.GetAsync(endpoint);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();

                    var jsonArray = JsonSerializer.Deserialize<JsonElement[]>(responseBody);
                    if (jsonArray == null) return new List<KlineData>();

                    return jsonArray.Select(k => new KlineData
                    {
                        OpenTime = k[0].GetInt64(),
                        Open = decimal.Parse(k[1].GetString()!),
                        High = decimal.Parse(k[2].GetString()!),
                        Low = decimal.Parse(k[3].GetString()!),
                        Close = decimal.Parse(k[4].GetString()!),
                        Volume = decimal.Parse(k[5].GetString()!),
                        CloseTime = k[6].GetInt64(),
                        QuoteAssetVolume = decimal.Parse(k[7].GetString()!),
                        NumberOfTrades = k[8].GetInt32(),
                        TakerBuyBaseAssetVolume = decimal.Parse(k[9].GetString()!),
                        TakerBuyQuoteAssetVolume = decimal.Parse(k[10].GetString()!)
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
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));

            try
            {
                var ticker24hr = await Get24hrTickerAsync(symbol);
                if (ticker24hr == null)
                {
                    _logger.LogWarning("Could not fetch 24hr ticker for {Symbol}, defaulting to HOLD", symbol);
                    return "HOLD";
                }

                var klineData = await GetKlineDataAsync(symbol);
                if (klineData.Count < 26)
                {
                    _logger.LogWarning("Insufficient kline data for {Symbol} (got {Count}, need 26+), defaulting to HOLD", 
                        symbol, klineData.Count);
                    return "HOLD";
                }

                var features = ExtractFeatures(symbol, ticker24hr, klineData);
                if (features == null)
                {
                    _logger.LogWarning("Could not extract features for {Symbol}, defaulting to HOLD", symbol);
                    return "HOLD";
                }

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
            try
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
                    RSI = (float)(decimal)features.RSI,
                    MovingAverage5 = (float)(float)(decimal)features.MovingAverage5,
                    MovingAverage20 = (float)(decimal)features.MovingAverage20,
                    BollingerUpper = (float)(decimal)features.BollingerUpper,
                    BollingerLower = (float)(decimal)features.BollingerLower,
                    MACD = (float)(decimal)features.MACD,
                    Signal = (float)(decimal)features.Signal,
                    VolumeRatio = (float)(decimal)features.VolumeRatio,
                    PriceChange24h = ticker.PriceChangePercentDecimal,
                    VolumeChange24h = (decimal)features.VolumeChange24h
                };

                await _marketDataRepository.SaveMarketDataAsync(marketData);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error saving market data for {Symbol}", symbol);
                // Don't throw - this shouldn't stop trading
            }
        }

        private CryptoFeatures? ExtractFeatures(string symbol, Ticker24hr ticker24hr, List<KlineData> klineData)
        {
            if (klineData.Count < 26)
            {
                _logger.LogWarning("Insufficient price history for {Symbol} (got {Count}, need 26+)", symbol, klineData.Count);
                return null;
            }

            try
            {
                var closePrices = klineData.Select(k => k.Close).ToList();
                var volumes = klineData.Select(k => k.Volume).ToList();

                var rsi = TechnicalAnalysis.CalculateRSI(closePrices);
                var ma5 = TechnicalAnalysis.CalculateMovingAverage(closePrices, 5);
                var ma20 = TechnicalAnalysis.CalculateMovingAverage(closePrices, 20);
                var (bollUpper, bollLower) = TechnicalAnalysis.CalculateBollingerBands(closePrices);
                var (macd, signal) = TechnicalAnalysis.CalculateMACD(closePrices);

                // FIX: Proper decimal to float conversion
                var volumeRatio = volumes.Count >= 2 ?
                    (volumes.Last() / volumes[^2]) : 1m;

                return new CryptoFeatures
                {
                    Price = (float)ticker24hr.LastPriceDecimal,
                    Volume = (float)ticker24hr.VolumeDecimal,
                    PriceChange24h = (float)ticker24hr.PriceChangePercentDecimal,
                    VolumeChange24h = (float)(decimal)volumeRatio, // Cast decimal to float
                    RSI = (float)rsi, // Cast decimal to float
                    MovingAverage5 = (float)ma5, // Cast decimal to float
                    MovingAverage20 = (float)ma20, // Cast decimal to float
                    BollingerUpper = (float)bollUpper, // Cast decimal to float
                    BollingerLower = (float)bollLower, // Cast decimal to float
                    MACD = (float)macd, // Cast decimal to float
                    Signal = (float)signal, // Cast decimal to float
                    VolumeRatio = (float)volumeRatio // Cast decimal to float
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error extracting features for {Symbol}", symbol);
                return null;
            }
        }

        public async Task<OrderResponse?> PlaceOrderAsync(OrderRequest orderRequest)
        {
            if (orderRequest == null)
                throw new ArgumentNullException(nameof(orderRequest));

            if (string.IsNullOrWhiteSpace(orderRequest.Symbol))
                throw new ArgumentException("Symbol cannot be null or empty", nameof(orderRequest));

            if (orderRequest.Quantity <= 0)
                throw new ArgumentException("Quantity must be greater than 0", nameof(orderRequest));

            var config = await GetEffectiveConfigAsync();
            if (config.PaperTradingMode)
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
                if (orderRequest.Price == null)
                    throw new ArgumentException("Price is required for LIMIT orders", nameof(orderRequest));

                queryParams.Append($"&timeInForce={orderRequest.TimeInForce.ToUpper()}");
                queryParams.Append($"&quantity={orderRequest.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                queryParams.Append($"&price={orderRequest.Price?.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }
            else if (orderRequest.Type == "MARKET")
            {
                queryParams.Append($"&quantity={orderRequest.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            queryParams.Append($"&timestamp={orderRequest.Timestamp}");

            string signature = await CreateSignatureAsync(queryParams.ToString());
            string fullQueryString = $"{queryParams.ToString()}&signature={signature}";

            var requestContent = new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded");

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var response = await _httpClient.PostAsync($"{endpoint}?{fullQueryString}", requestContent);
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
            await ConfigureHttpClientAsync(); // Ensure HTTP client is configured with latest config
            
            string endpoint = "/api/v3/account";
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string queryString = $"timestamp={timestamp}";

            string signature = await CreateSignatureAsync(queryString);
            string fullQueryString = $"{queryString}&signature={signature}";

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var response = await _httpClient.GetAsync($"{endpoint}?{fullQueryString}");
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

        // Add proper disposal
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

}