using TradeNetics.Shared.Models;

namespace TradeNetics.Shared.Interfaces
{
    public interface ICryptoTraderService
    {
        Task<TickerPrice?> GetPriceAsync(string symbol);
        Task<Ticker24hr?> Get24hrTickerAsync(string symbol);
        Task<List<KlineData>> GetKlineDataAsync(string symbol, string interval = "1h", int limit = 100);
        Task<OrderResponse?> PlaceOrderAsync(OrderRequest orderRequest);
        Task<AccountBalance[]?> GetAccountBalancesAsync();
        Task<string> GetMLPredictionAsync(string symbol);
    }
}
