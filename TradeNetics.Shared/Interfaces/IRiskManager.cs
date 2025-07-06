using TradeNetics.Shared.Models;

namespace TradeNetics.Shared.Interfaces
{
    public interface IRiskManager
    {
        bool CanPlaceOrder(OrderRequest order, decimal portfolioValue);
        decimal CalculatePositionSize(string symbol, decimal confidence, decimal portfolioValue);
        bool IsStopLossTriggered(string symbol, decimal currentPrice, decimal entryPrice, string side);
    }
}