using System.ComponentModel.DataAnnotations;

namespace TradeNetics.Shared.Models
{
    public class PortfolioHolding
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal MarketValue { get; set; }
        public decimal PnL { get; set; }
        public decimal PnLPercent { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool IsStableCoin { get; set; }
        public decimal Change24h { get; set; }
        public decimal AllocationPercentage { get; set; }
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
        public decimal UsdValue { get; set; }
    }
}