using System.ComponentModel.DataAnnotations;

namespace TradeNetics.Shared.Models
{
    public class TradingBotStatus
    {
        public bool IsRunning { get; set; }
        public DateTime LastUpdate { get; set; }
        public string CurrentStrategy { get; set; } = "";
        public decimal TotalPnL { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal DailyPnL { get; set; }
        public int TotalTrades { get; set; }
        public int DailyTrades { get; set; }
        public decimal PortfolioValue { get; set; }
        public decimal AvailableBalance { get; set; }
        public string Status { get; set; } = "";
        public string LastError { get; set; } = "";
        public DateTime LastTradeTime { get; set; }
        public float ModelConfidence { get; set; }
        public string ActiveSymbols { get; set; } = "";
        public bool PaperTradingMode { get; set; }
    }
}