namespace TradeNetics.Shared.Models
{
    public class Ticker24hr
    {
        public string Symbol { get; set; } = "";
        public string PriceChange { get; set; } = "0";
        public string PriceChangePercent { get; set; } = "0";
        public string WeightedAvgPrice { get; set; } = "0";
        public string PrevClosePrice { get; set; } = "0";
        public string LastPrice { get; set; } = "0";
        public string LastQty { get; set; } = "0";
        public string BidPrice { get; set; } = "0";
        public string AskPrice { get; set; } = "0";
        public string OpenPrice { get; set; } = "0";
        public string HighPrice { get; set; } = "0";
        public string LowPrice { get; set; } = "0";
        public string Volume { get; set; } = "0";
        public string QuoteVolume { get; set; } = "0";
        public long OpenTime { get; set; }
        public long CloseTime { get; set; }
        public long FirstId { get; set; }
        public long LastId { get; set; }
        public long Count { get; set; }

        // Helper properties to get decimal values
        public decimal LastPriceDecimal => decimal.Parse(LastPrice);
        public decimal PriceChangePercentDecimal => decimal.Parse(PriceChangePercent);
        public decimal VolumeDecimal => decimal.Parse(Volume);
        public decimal HighPriceDecimal => decimal.Parse(HighPrice);
        public decimal LowPriceDecimal => decimal.Parse(LowPrice);
        public decimal OpenPriceDecimal => decimal.Parse(OpenPrice);
    }
}