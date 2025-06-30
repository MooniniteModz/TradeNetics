using TradeNetics.Models;
using TradeNetics.Data;

namespace TradeNetics.Interfaces
{
    public interface IMarketDataRepository
    {
        Task SaveMarketDataAsync(MarketData marketData);
        Task<List<MarketData>> GetMarketDataAsync(string symbol, DateTime from, DateTime to);
        Task<List<TrainingData>> GetTrainingDataAsync();
    }

    public interface ICryptoTraderService
    {
        Task<TickerPrice?> GetPriceAsync(string symbol);
        Task<Ticker24hr?> Get24hrTickerAsync(string symbol);
        Task<List<KlineData>> GetKlineDataAsync(string symbol, string interval = "1h", int limit = 100);
        Task<OrderResponse?> PlaceOrderAsync(OrderRequest orderRequest);
        Task<AccountBalance[]?> GetAccountBalancesAsync();
        Task<string> GetMLPredictionAsync(string symbol);
    }

    public interface IRiskManager
    {
        bool CanPlaceOrder(OrderRequest order, decimal portfolioValue);
        decimal CalculatePositionSize(string symbol, decimal confidence, decimal portfolioValue);
        bool IsStopLossTriggered(string symbol, decimal currentPrice, decimal entryPrice, string side);
    }

    public interface IPortfolioManager
    {
        Task<Portfolio> GetCurrentPortfolioAsync();
        Task SavePortfolioSnapshotAsync();
        Task<decimal> CalculatePnLAsync();
    }

    public interface IMLTradingModel
    {
        Task TrainModelAsync(List<TrainingData> trainingData);
        TradingPrediction PredictAction(CryptoFeatures features);
        Task SaveModelAsync(string filePath);
        Task LoadModelAsync(string filePath);
    }
}
