using Microsoft.Extensions.Logging;
using TradeNetics.Interfaces;
using TradeNetics.Models;

namespace TradeNetics.Services
{
    public class RiskManager : IRiskManager
    {
        private readonly TradingConfiguration _config;
        private readonly ILogger<RiskManager> _logger;

        public RiskManager(TradingConfiguration config, ILogger<RiskManager> logger)
        {
            _config = config;
            _logger = logger;
        }

        public bool CanPlaceOrder(OrderRequest order, decimal portfolioValue)
        {
            var positionValue = order.Quantity * (order.Price ?? 0);
            var positionSizePercent = positionValue / portfolioValue;

            if (positionSizePercent > _config.MaxPositionSize)
            {
                _logger.LogWarning("Order rejected: Position size {Size}% exceeds maximum {Max}%",
                    positionSizePercent * 100, _config.MaxPositionSize * 100);
                return false;
            }

            return true;
        }

        public decimal CalculatePositionSize(string symbol, decimal confidence, decimal portfolioValue)
        {
            // Kelly Criterion-based position sizing
            var baseSize = _config.MaxPositionSize * portfolioValue;
            var adjustedSize = baseSize * confidence;

            return Math.Min(adjustedSize, _config.MaxPositionSize * portfolioValue);
        }

        public bool IsStopLossTriggered(string symbol, decimal currentPrice, decimal entryPrice, string side)
        {
            if (side.ToUpper() == "BUY")
            {
                var lossPercent = (entryPrice - currentPrice) / entryPrice;
                return lossPercent >= _config.StopLossPercent;
            }
            else if (side.ToUpper() == "SELL")
            {
                var lossPercent = (currentPrice - entryPrice) / entryPrice;
                return lossPercent >= _config.StopLossPercent;
            }

            return false;
        }
    }
}
