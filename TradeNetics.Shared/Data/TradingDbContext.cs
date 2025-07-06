using Microsoft.EntityFrameworkCore;
using TradeNetics.Shared.Models;

namespace TradeNetics.Shared.Data
{
    public class TradingDbContext : DbContext
    {
        public TradingDbContext(DbContextOptions<TradingDbContext> options) : base(options) { }

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