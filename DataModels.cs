using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TradeNetics.Data
{
    public class MarketData
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public float RSI { get; set; }
        public float MovingAverage5 { get; set; }
        public float MovingAverage20 { get; set; }
        public float BollingerUpper { get; set; }
        public float BollingerLower { get; set; }
        public float MACD { get; set; }
        public float Signal { get; set; }
        public float VolumeRatio { get; set; }
        public decimal PriceChange24h { get; set; }
        public decimal VolumeChange24h { get; set; }
    }

    public class TradeRecord
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = "";
        public string Side { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime ExecutedAt { get; set; }
        public string MLPrediction { get; set; } = "";
        public decimal PnL { get; set; }
        public decimal PortfolioValueBefore { get; set; }
        public decimal PortfolioValueAfter { get; set; }
        public string OrderId { get; set; } = "";
        public bool IsPaperTrade { get; set; }
        public float ConfidenceScore { get; set; }
    }

    public class ModelPerformance
    {
        public int Id { get; set; }
        public DateTime TrainingDate { get; set; }
        public double Accuracy { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1Score { get; set; }
        public int TrainingDataCount { get; set; }
        public string ModelVersion { get; set; } = "";
    }

    public class PortfolioSnapshot
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal TotalValue { get; set; }
        public decimal DailyPnL { get; set; }
        public decimal TotalPnL { get; set; }
        public string AssetAllocation { get; set; } = ""; // JSON string
        public decimal RiskScore { get; set; }
    }

    public class TradingContext : DbContext
    {
        public TradingContext(DbContextOptions<TradingContext> options) : base(options) { }

        public DbSet<MarketData> MarketData { get; set; }
        public DbSet<TradeRecord> TradeRecords { get; set; }
        public DbSet<ModelPerformance> ModelPerformances { get; set; }
        public DbSet<PortfolioSnapshot> PortfolioSnapshots { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MarketData>()
                .HasIndex(m => new { m.Symbol, m.Timestamp })
                .IsUnique();

            modelBuilder.Entity<TradeRecord>()
                .HasIndex(t => t.ExecutedAt);

            modelBuilder.Entity<PortfolioSnapshot>()
                .HasIndex(p => p.Timestamp);

            // Configure decimal precision for financial data
            modelBuilder.Entity<MarketData>()
                .Property(m => m.Close)
                .HasPrecision(18, 8);

            modelBuilder.Entity<TradeRecord>()
                .Property(t => t.Price)
                .HasPrecision(18, 8);
        }
    }
}
