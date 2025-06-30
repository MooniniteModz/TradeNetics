using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeNetics.Data;
using TradeNetics.Interfaces;
using TradeNetics.Models;

namespace TradeNetics.Services
{
    public class MarketDataRepository : IMarketDataRepository
    {
        private readonly TradingContext _context;
        private readonly ILogger<MarketDataRepository> _logger;

        public MarketDataRepository(TradingContext context, ILogger<MarketDataRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SaveMarketDataAsync(MarketData marketData)
        {
            try
            {
                var existing = await _context.MarketData
                    .FirstOrDefaultAsync(m => m.Symbol == marketData.Symbol && m.Timestamp == marketData.Timestamp);

                if (existing == null)
                {
                    _context.MarketData.Add(marketData);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Update existing record
                    existing.Close = marketData.Close;
                    existing.Volume = marketData.Volume;
                    existing.RSI = marketData.RSI;
                    existing.MACD = marketData.MACD;
                    // Update other fields...
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving market data for {Symbol}", marketData.Symbol);
                throw;
            }
        }

        public async Task<List<MarketData>> GetMarketDataAsync(string symbol, DateTime from, DateTime to)
        {
            return await _context.MarketData
                .Where(m => m.Symbol == symbol && m.Timestamp >= from && m.Timestamp <= to)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }

        public async Task<List<TrainingData>> GetTrainingDataAsync()
        {
            var marketData = await _context.MarketData
                .Where(m => m.Timestamp >= DateTime.UtcNow.AddDays(-30))
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            return marketData.Select(m => new TrainingData
            {
                Price = (float)m.Close,
                Volume = (float)m.Volume,
                PriceChange24h = (float)m.PriceChange24h,
                VolumeChange24h = (float)m.VolumeChange24h,
                RSI = m.RSI,
                MovingAverage5 = m.MovingAverage5,
                MovingAverage20 = m.MovingAverage20,
                BollingerUpper = m.BollingerUpper,
                BollingerLower = m.BollingerLower,
                MACD = m.MACD,
                Signal = m.Signal,
                VolumeRatio = m.VolumeRatio,
                Label = DetermineLabel(m) // Generate labels based on future price movement
            }).ToList();
        }

        private string DetermineLabel(MarketData data)
        {
            // Simple labeling logic - you can improve this
            if (data.RSI < 30 && data.PriceChange24h < -2) return "BUY";
            if (data.RSI > 70 && data.PriceChange24h > 2) return "SELL";
            return "HOLD";
        }
    }
}
