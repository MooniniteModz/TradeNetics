using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using TradeNetics.Data;
using TradeNetics.Helpers;
using TradeNetics.Interfaces;
using TradeNetics.Models;

namespace TradeNetics.Services
{
    public class CryptoTraderService : ICryptoTraderService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly TradingConfiguration _config;
        private readonly IMLTradingModel _mlModel;
        private readonly IMarketDataRepository _marketDataRepository;
        private readonly ILogger<CryptoTraderService> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;

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
}
