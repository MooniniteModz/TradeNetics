namespace TradeNetics.Shared.Models
{
    public class TickerPrice
    {
        public string Symbol { get; set; } = "";
        public string Price { get; set; } = "0"; // Binance returns as string

        // Helper property to get decimal value
        public decimal PriceDecimal => decimal.Parse(Price);
    }
}