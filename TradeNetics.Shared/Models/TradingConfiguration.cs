namespace TradeNetics.Shared.Models
{
    public class TradingConfiguration
    {
        public string ApiKey { get; set; } = "";
        public string ApiSecret { get; set; } = "";
        public string BaseApiUrl { get; set; } = "https://api.binance.us";
        public Dictionary<string, decimal> SymbolQuantities { get; set; } = new();
        public decimal MinConfidenceScore { get; set; } = 0.7m;
        public bool PaperTradingMode { get; set; } = true;
        public bool TradingEnabled { get; set; } = false;
        public TimeSpan ModelRetrainingInterval { get; set; } = TimeSpan.FromDays(7);
        public decimal MaxPositionSize { get; set; } = 0.02m; // 2% of portfolio
        public decimal StopLossPercent { get; set; } = 0.05m; // 5% stop loss
        public decimal MaxDailyLoss { get; set; } = 0.10m; // 10% daily loss limit
        public string[] TradingSymbols { get; set; } = { "BTCUSDT", "ETHUSDT", "ADAUSDT" };
        public decimal BacktestInitialBalance { get; set; } = 10000m;
    }

    public enum TradeAction
    {
        BUY,
        SELL,
        HOLD
    }

    public enum OrderType
    {
        LIMIT,
        MARKET
    }
}
