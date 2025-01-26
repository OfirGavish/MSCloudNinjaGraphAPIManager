using System.Net.Http.Headers;
using System.Text.Json;
using MSCloudNinjaGraphAPI.Services.Interfaces;

namespace MSCloudNinjaGraphAPI.Services
{
    public class SsoSettingsService
    {
        private readonly HttpClient _httpClient;
        private readonly IBackupComponent _backupComponent;

        public SsoSettingsService(IBackupComponent backupComponent)
        {
            _httpClient = new HttpClient();
            _backupComponent = backupComponent;
        }

        public async Task<string> GetSsoSettingsId(string clientId, string accessToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.GetAsync($"https://graph.microsoft.com/v1.0/servicePrincipals?$filter=appId eq '{clientId}'&$select=id");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStreamAsync();
            var settings = await JsonSerializer.DeserializeAsync<JsonElement>(content);
            var servicePrincipalId = settings.GetProperty("value")[0].GetProperty("id").GetString();

            response = await _httpClient.GetAsync($"https://graph.microsoft.com/v1.0/servicePrincipals/{servicePrincipalId}/synchronization/secrets");
            response.EnsureSuccessStatusCode();

            content = await response.Content.ReadAsStreamAsync();
            settings = await JsonSerializer.DeserializeAsync<JsonElement>(content);
            return settings.GetProperty("value")[0].GetProperty("id").GetString();
        }

        public async Task<JsonDocument> GetSsoConfiguration(string clientId, string servicePrincipalId, string accessToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.GetAsync($"https://graph.microsoft.com/v1.0/servicePrincipals/{servicePrincipalId}/synchronization/settings");
            response.EnsureSuccessStatusCode();
            
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        }

        public async Task BackupSsoConfiguration(string clientId, string accessToken)
        {
            try
            {
                var servicePrincipalId = await GetSsoSettingsId(clientId, accessToken);
                var ssoConfig = await GetSsoConfiguration(clientId, servicePrincipalId, accessToken);
                
                await _backupComponent.AddConfiguration(clientId, ssoConfig);
                await _backupComponent.SaveAllConfigurations();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to backup SSO configuration: {ex.Message}", ex);
            }
        }
    }
}
