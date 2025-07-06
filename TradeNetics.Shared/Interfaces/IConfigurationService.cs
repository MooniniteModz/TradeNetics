using TradeNetics.Shared.Models;
using System.Threading.Tasks;

namespace TradeNetics.Shared.Interfaces
{
    public interface IConfigurationService
    {
        Task<TradingConfiguration> GetConfiguration();
        Task SaveConfiguration(TradingConfiguration configuration);
    }
}