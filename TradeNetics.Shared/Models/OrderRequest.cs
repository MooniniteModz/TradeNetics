using TradeNetics.Shared.Models;

namespace TradeNetics.Shared.Models
{
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
}
