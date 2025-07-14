using System.Collections.Generic;
using System.Threading.Tasks;
using TradeNetics.Shared.Models;

namespace TradeNetics.WebApp.Data
{
    public interface ICryptoDataService
    {
        Task<List<MarketData>> GetMarketDataAsync();
        Task<List<PortfolioHolding>> GetPortfolioHoldingsAsync();
        Task<List<TradeData>> GetRecentTradesAsync();
        Task<TradingBotStatus> GetBotStatusAsync();
    }
}