
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TradeNetics.Shared.Interfaces;
using TradeNetics.Shared.Models;

namespace TradeNetics.Shared.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly string _configurationPath = Path.Combine(Environment.CurrentDirectory, "appsettings.json");

        public async Task<TradingConfiguration> GetConfiguration()
        {
            if (!File.Exists(_configurationPath))
            {
                return new TradingConfiguration();
            }

            var json = await File.ReadAllTextAsync(_configurationPath);
            return JsonSerializer.Deserialize<TradingConfiguration>(json) ?? new TradingConfiguration();
        }

        public async Task SaveConfiguration(TradingConfiguration configuration)
        {
            var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configurationPath, json);
        }
    }
}
