using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradeNetics.Shared.Interfaces;
using TradeNetics.Shared.Models;

namespace TradeNetics.WebApp.Data
{
    public class RealCryptoDataService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationService _configurationService;
        private readonly ILogger<RealCryptoDataService> _logger;
        private TradingConfiguration? _config;

        public RealCryptoDataService(HttpClient httpClient, IConfigurationService configurationService, ILogger<RealCryptoDataService> logger)
        {
            _httpClient = httpClient;
            _configurationService = configurationService;
            _logger = logger;
        }

        private async Task<TradingConfiguration> GetConfigurationAsync()
        {
            if (_config == null)
            {
                _config = await _configurationService.GetConfiguration();
                ConfigureHttpClient();
            }
            return _config;
        }

        private void ConfigureHttpClient()
        {
            if (_config != null && !string.IsNullOrEmpty(_config.ApiKey))
            {
                _httpClient.BaseAddress = new Uri(_config.BaseApiUrl);
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", _config.ApiKey);
            }
        }

        public async Task<TradingBotStatus> GetBotStatusAsync()
        {
            // For now, return a status based on whether we can connect to the API
            try
            {
                var config = await GetConfigurationAsync();
                if (string.IsNullOrEmpty(config.ApiKey) || string.IsNullOrEmpty(config.ApiSecret))
                {
                    return new TradingBotStatus
                    {
                        IsRunning = false,
                        Status = "API credentials not configured",
                        TotalProfit = 0,
                        TotalTrades = 0,
                        LastUpdate = DateTime.Now
                    };
                }

                // Try to connect to API to verify credentials
                var accountInfo = await GetAccountInfoAsync();
                return new TradingBotStatus
                {
                    IsRunning = accountInfo != null,
                    Status = accountInfo != null ? "Connected to Binance" : "API connection failed",
                    TotalProfit = 0, // Would need to calculate from trade history
                    TotalTrades = 0, // Would need to get from trade history  
                    LastUpdate = DateTime.Now
                };
            }
            catch
            {
                return new TradingBotStatus
                {
                    IsRunning = false,
                    Status = "Error connecting to API",
                    TotalProfit = 0,
                    TotalTrades = 0,
                    LastUpdate = DateTime.Now
                };
            }
        }

        public async Task<List<TradeData>> GetRecentTradesAsync()
        {
            try
            {
                var config = await GetConfigurationAsync();
                if (string.IsNullOrEmpty(config.ApiKey) || string.IsNullOrEmpty(config.ApiSecret))
                {
                    return new List<TradeData>();
                }

                // For now, return empty list - would need to implement actual trade history API
                // This would require calling GET /api/v3/myTrades for each symbol
                return new List<TradeData>();
            }
            catch
            {
                return new List<TradeData>();
            }
        }

        public async Task<List<PortfolioHolding>> GetPortfolioHoldingsAsync()
        {
            try
            {
                var config = await GetConfigurationAsync();
                
                if (string.IsNullOrEmpty(config.ApiKey) || string.IsNullOrEmpty(config.ApiSecret))
                {
                    _logger.LogWarning("API credentials not configured, returning empty portfolio");
                    return new List<PortfolioHolding>();
                }

                // Get account information from Binance
                var accountInfo = await GetAccountInfoAsync();
                if (accountInfo == null) return new List<PortfolioHolding>();

                var holdings = new List<PortfolioHolding>();
                
                // Filter balances with meaningful amounts
                var significantBalances = accountInfo.Balances?.Where(b => decimal.Parse(b.Free) + decimal.Parse(b.Locked) > 0.001m) ?? new List<BinanceBalance>();

                foreach (var balance in significantBalances)
                {
                    var amount = decimal.Parse(balance.Free) + decimal.Parse(balance.Locked);
                    if (amount <= 0) continue;

                    // Get current price for this asset
                    var symbol = balance.Asset == "USDT" ? "BTCUSDT" : $"{balance.Asset}USDT";
                    var price = await GetCurrentPriceAsync(symbol);
                    var change24h = await Get24hChangeAsync(symbol);

                    holdings.Add(new PortfolioHolding
                    {
                        Symbol = balance.Asset,
                        Name = GetAssetName(balance.Asset),
                        Amount = amount,
                        Price = balance.Asset == "USDT" ? 1.0m : price,
                        Change24h = change24h
                    });
                }

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

                return holdings.OrderByDescending(h => h.UsdValue).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching real portfolio holdings");
                return new List<PortfolioHolding>();
            }
        }

        private async Task<BinanceAccountInfo?> GetAccountInfoAsync()
        {
            try
            {
                var config = await GetConfigurationAsync();
                var queryString = $"timestamp={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var signature = CreateSignature(queryString, config.ApiSecret);
                
                var response = await _httpClient.GetAsync($"/api/v3/account?{queryString}&signature={signature}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<BinanceAccountInfo>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                else
                {
                    _logger.LogWarning("Failed to get account info: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account info");
                return null;
            }
        }

        private async Task<decimal> GetCurrentPriceAsync(string symbol)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v3/ticker/price?symbol={symbol}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var priceData = JsonSerializer.Deserialize<BinancePrice>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return decimal.Parse(priceData?.Price ?? "0");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting price for {Symbol}", symbol);
            }
            return 0;
        }

        private async Task<decimal> Get24hChangeAsync(string symbol)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v3/ticker/24hr?symbol={symbol}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var tickerData = JsonSerializer.Deserialize<Binance24hrTicker>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return decimal.Parse(tickerData?.PriceChangePercent ?? "0");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting 24h change for {Symbol}", symbol);
            }
            return 0;
        }

        private static string CreateSignature(string message, string secretKey)
        {
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(secretKey);
            var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
            
            using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(messageBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private static string GetAssetName(string asset)
        {
            return asset switch
            {
                "BTC" => "Bitcoin",
                "ETH" => "Ethereum", 
                "ADA" => "Cardano",
                "LTC" => "Litecoin",
                "DOGE" => "Dogecoin",
                "USDT" => "Tether USD",
                _ => asset
            };
        }
    }

    public class BinanceAccountInfo
    {
        public List<BinanceBalance>? Balances { get; set; }
    }

    public class BinanceBalance
    {
        public string Asset { get; set; } = "";
        public string Free { get; set; } = "0";
        public string Locked { get; set; } = "0";
    }

    public class BinancePrice
    {
        public string Symbol { get; set; } = "";
        public string Price { get; set; } = "0";
    }

    public class Binance24hrTicker
    {
        public string Symbol { get; set; } = "";
        public string PriceChangePercent { get; set; } = "0";
    }
}