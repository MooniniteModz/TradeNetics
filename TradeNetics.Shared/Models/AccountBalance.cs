namespace TradeNetics.Shared.Models
{
    public class AccountBalance
    {
        public string Asset { get; set; } = "";
        public decimal Free { get; set; }
        public decimal Locked { get; set; }
        public decimal Total => Free + Locked;
    }
}