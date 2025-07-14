
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
        private readonly string _configurationPath;

        public ConfigurationService()
        {
            // Use a shared location for configuration that both apps can access
            var baseDirectory = Path.GetDirectoryName(Environment.CurrentDirectory);
            if (baseDirectory != null && Directory.Exists(Path.Combine(baseDirectory, "TradeNetics.WebApp")))
            {
                // Running from Console directory, point to WebApp config
                _configurationPath = Path.Combine(baseDirectory, "TradeNetics.WebApp", "appsettings.json");
                Console.WriteLine($"ConfigurationService: Using shared config path: {_configurationPath}");
            }
            else
            {
                // Running from WebApp or other directory, use current directory
                _configurationPath = Path.Combine(Environment.CurrentDirectory, "appsettings.json");
                Console.WriteLine($"ConfigurationService: Using local config path: {_configurationPath}");
            }
        }

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
