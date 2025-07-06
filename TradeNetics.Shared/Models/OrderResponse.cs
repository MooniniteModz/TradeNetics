namespace TradeNetics.Shared.Models
{
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
}