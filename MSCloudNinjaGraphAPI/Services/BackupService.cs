using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MSCloudNinjaGraphAPI.Services
{
    public class BackupService<T>
    {
        private readonly string _backupType;

        public BackupService(string backupType)
        {
            _backupType = backupType;
        }

        public async Task BackupToFileAsync(List<T> items, string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(items, options);
                await File.WriteAllTextAsync(filePath, jsonString);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to backup {_backupType}: {ex.Message}", ex);
            }
        }

        public async Task<List<T>> RestoreFromFileAsync(string filePath)
        {
            try
            {
                string jsonString = await File.ReadAllTextAsync(filePath);
                var items = JsonSerializer.Deserialize<List<T>>(jsonString);
                return items ?? new List<T>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to restore {_backupType} from backup: {ex.Message}", ex);
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
    }
}
