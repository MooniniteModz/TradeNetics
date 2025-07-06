using TradeNetics.Shared.Models;
using System.Threading.Tasks;

namespace TradeNetics.Shared.Interfaces
{
    public interface IPortfolioManager
    {
        Task<Portfolio> GetCurrentPortfolioAsync();
        Task SavePortfolioSnapshotAsync();
        Task<decimal> CalculatePnLAsync();
    }
}