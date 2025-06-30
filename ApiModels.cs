namespace TradeNetics.Models
{
    public class TickerPrice
    {
        public string Symbol { get; set; } = "";
        public decimal Price { get; set; }
    }

    public class Ticker24hr
    {
        public string Symbol { get; set; } = "";
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

    public class OrderResponse
    {
        public string Symbol { get; set; } = "";
        public long OrderId { get; set; }
        public string ClientOrderId { get; set; } = "";
        public long TransactTime { get; set; }
        public decimal Price { get; set; }
        public decimal OrigQty { get; set; }
        public decimal ExecutedQty { get; set; }
        public decimal CummulativeQuoteQty { get; set; }
        public string Status { get; set; } = "";
        public string TimeInForce { get; set; } = "";
        public string Type { get; set; } = "";
        public string Side { get; set; } = "";
    }

    public class AccountBalance
    {
        public string Asset { get; set; } = "";
        public decimal Free { get; set; }
        public decimal Locked { get; set; }
        public decimal Total => Free + Locked;
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
