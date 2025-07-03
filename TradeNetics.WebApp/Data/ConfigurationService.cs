
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TradeNetics.WebApp.Data
{
    public class ConfigurationService
    {
        private readonly string _configurationPath = Path.Combine(Environment.CurrentDirectory, "appsettings.json");

        public async Task<ConfigurationModel> GetConfiguration()
        {
            if (!File.Exists(_configurationPath))
            {
                return new ConfigurationModel();
            }

            var json = await File.ReadAllTextAsync(_configurationPath);
            return JsonSerializer.Deserialize<ConfigurationModel>(json);
        }

        public async Task SaveConfiguration(ConfigurationModel configuration)
        {
            var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configurationPath, json);
        }
    }
}
