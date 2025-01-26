using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using MSCloudNinjaGraphAPI.Services.Interfaces;

namespace MSCloudNinjaGraphAPI.Services
{
    public class BackupService<T> : IBackupComponent
    {
        private readonly string _backupType;
        private readonly Dictionary<string, JsonDocument> _ssoConfigurations = new();
        private readonly List<T> _items = new();

        public BackupService(string backupType)
        {
            _backupType = backupType;
        }

        public async Task BackupToFileAsync(List<object> items, string filePath)
        {
            try
            {
                _items.Clear();
                _items.AddRange(items.Cast<T>());
                await SaveAllConfigurations();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to backup {_backupType}: {ex.Message}", ex);
            }
        }

        public async Task<List<object>> RestoreFromFileAsync(string filePath)
        {
            try
            {
                string jsonString = await File.ReadAllTextAsync(filePath);
                var backupData = JsonSerializer.Deserialize<BackupData<T>>(jsonString);
                
                _ssoConfigurations.Clear();
                if (backupData.SsoConfigurations != null)
                {
                    foreach (var kvp in backupData.SsoConfigurations)
                    {
                        _ssoConfigurations[kvp.Key] = JsonDocument.Parse(kvp.Value.ToString());
                    }
                }

                return backupData.Items.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to restore {_backupType} from backup: {ex.Message}", ex);
            }
        }

        public async Task AddConfiguration(string clientId, JsonDocument ssoConfig)
        {
            _ssoConfigurations[clientId] = ssoConfig;
        }

        public async Task SaveAllConfigurations()
        {
            try
            {
                var backupData = new BackupData<T>
                {
                    Items = _items,
                    SsoConfigurations = _ssoConfigurations.ToDictionary(
                        kvp => kvp.Key,
                        kvp => JsonSerializer.Deserialize<JsonElement>(kvp.Value.RootElement.GetRawText())
                    )
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(backupData, options);
                string filePath = GetDefaultFilePath(Path.GetDirectoryName(GetDefaultFileName()));
                await File.WriteAllTextAsync(filePath, jsonString);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save configurations: {ex.Message}", ex);
            }
        }

        public string GetDefaultFileName()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return $"{_backupType}_{timestamp}.json";
        }

        public string GetDefaultFilePath(string directory)
        {
            return Path.Combine(directory, GetDefaultFileName());
        }

        private class BackupData<TItem>
        {
            public List<TItem> Items { get; set; }
            public Dictionary<string, JsonElement> SsoConfigurations { get; set; }
        }
    }
}
