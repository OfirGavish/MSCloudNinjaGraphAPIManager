using System.Text.Json;
using System.Collections.Generic;

namespace MSCloudNinjaGraphAPI.Services.Interfaces
{
    public interface IBackupComponent
    {
        Task BackupToFileAsync(List<object> items, string filePath);
        Task<List<object>> RestoreFromFileAsync(string filePath);
        Task AddConfiguration(string clientId, JsonDocument ssoConfig);
        Task SaveAllConfigurations();
    }
}
