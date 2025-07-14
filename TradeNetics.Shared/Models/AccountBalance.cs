namespace TradeNetics.Shared.Models
{
    public class AccountBalance
    {
        public string Asset { get; set; } = "";
        public string Free { get; set; } = "0";
        public string Locked { get; set; } = "0";

        // Helper properties to get decimal values
        public decimal FreeDecimal => decimal.Parse(Free);
        public decimal LockedDecimal => decimal.Parse(Locked);
        public decimal TotalDecimal => FreeDecimal + LockedDecimal;
    }
}