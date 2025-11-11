namespace TradeNetics.Shared.Models
{
    public class TradeRecord
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = "";
        public string Side { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal? StopLoss { get; set; }
        public DateTime ExecutedAt { get; set; }
        public string MLPrediction { get; set; } = "";
        public decimal PnL { get; set; }
        public decimal PortfolioValueBefore { get; set; }
        public decimal PortfolioValueAfter { get; set; }
        public string OrderId { get; set; } = "";
        public bool IsPaperTrade { get; set; }
        public float ConfidenceScore { get; set; }
    }
}
