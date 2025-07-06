using TradeNetics.Shared.Models;

namespace TradeNetics.Shared.Interfaces
{
    public interface IMarketDataRepository
    {
        Task SaveMarketDataAsync(MarketData marketData);
        Task<List<MarketData>> GetMarketDataAsync(string symbol, DateTime from, DateTime to);
        Task<List<TrainingData>> GetTrainingDataAsync();
    }
}