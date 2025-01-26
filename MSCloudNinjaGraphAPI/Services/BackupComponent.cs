using System.Text.Json;
using MSCloudNinjaGraphAPI.Services.Interfaces;

namespace MSCloudNinjaGraphAPI.Services
{
    public class BackupComponent : Interfaces.IBackupComponent
    {
        private readonly Dictionary<string, JsonDocument> _configurations = new();
        private readonly List<object> _items = new();
        
        public async Task BackupToFileAsync(List<object> items, string filePath)
        {
            _items.Clear();
            _items.AddRange(items);
            await SaveAllConfigurations();
        }

        public async Task<List<object>> RestoreFromFileAsync(string filePath)
        {
            try
            {
                string jsonString = await File.ReadAllTextAsync(filePath);
                var data = JsonSerializer.Deserialize<BackupData>(jsonString);
                
                _configurations.Clear();
                if (data.SsoConfigurations != null)
                {
                    foreach (var kvp in data.SsoConfigurations)
                    {
                        _configurations[kvp.Key] = JsonDocument.Parse(kvp.Value.ToString());
                    }
                }

                return data.Items ?? new List<object>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to restore from backup: {ex.Message}", ex);
            }
        }
        
        public async Task AddConfiguration(string clientId, JsonDocument ssoConfig)
        {
            _configurations[clientId] = ssoConfig;
        }
        
        public async Task SaveAllConfigurations()
        {
            try
            {
                var backupData = new BackupData
                {
                    Items = _items,
                    SsoConfigurations = _configurations.ToDictionary(
                        kvp => kvp.Key,
                        kvp => JsonSerializer.Deserialize<JsonElement>(kvp.Value.RootElement.GetRawText())
                    )
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(backupData, options);
                await File.WriteAllTextAsync("backup.json", jsonString);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save configurations: {ex.Message}", ex);
            }
        }

        private class BackupData
        {
            public List<object> Items { get; set; }
            public Dictionary<string, JsonElement> SsoConfigurations { get; set; }
        }
    }
}
