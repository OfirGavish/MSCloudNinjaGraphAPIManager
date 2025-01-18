using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.ServicePrincipals.Item.AddPassword;
using Microsoft.Graph.ServicePrincipals.Item.AddKey;
using GraphApplication = Microsoft.Graph.Models.Application;

namespace MSCloudNinjaGraphAPI.Services
{
    public class ApplicationBackup
    {
        [JsonPropertyName("application")]
        public GraphApplication Application { get; set; }
        [JsonPropertyName("backupDate")]
        public DateTime BackupDate { get; set; }
        [JsonPropertyName("servicePrincipal")]
        public ServicePrincipal ServicePrincipal { get; set; }
        [JsonPropertyName("secrets")]
        public List<PasswordCredential> Secrets { get; set; }
        [JsonPropertyName("certificates")]
        public List<KeyCredential> Certificates { get; set; }
        [JsonPropertyName("syncJob")]
        public SynchronizationJob SyncJob { get; set; }
        [JsonPropertyName("syncTemplate")]
        public SynchronizationTemplate SyncTemplate { get; set; }
    }

    public class EnterpriseAppsService : IEnterpriseAppsService
    {
        private readonly GraphServiceClient _graphClient;
        private const int PageSize = 999;
        private readonly string _logFilePath;

        public EnterpriseAppsService(GraphServiceClient graphClient)
        {
            _graphClient = graphClient;
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "restore_log.txt");
        }

        private async Task LogAsync(string message)
        {
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(_logFilePath, logMessage);
        }

        public async Task<List<GraphApplication>> GetApplicationsAsync()
        {
            try
            {
                var apps = new List<GraphApplication>();
                var response = await _graphClient.Applications.GetAsync(config =>
                {
                    config.QueryParameters.Top = PageSize;
                    config.Headers.Add("ConsistencyLevel", "eventual");
                    config.QueryParameters.Count = true;
                    config.QueryParameters.Select = new[]
                    {
                        "id", "appId", "displayName", "description", "notes",
                        "publisherDomain", "signInAudience", "identifierUris",
                        "web", "spa", "publicClient", "requiredResourceAccess", "api",
                        "appRoles", "info", "isDeviceOnlyAuthSupported",
                        "isFallbackPublicClient", "tags", "certification",
                        "disabledByMicrosoftStatus", "groupMembershipClaims",
                        "optionalClaims", "parentalControlSettings", "publicClient",
                        "requestSignatureVerification", "servicePrincipalLockConfiguration",
                        "tokenEncryptionKeyId", "verifiedPublisher", "defaultRedirectUri",
                        "synchronization"
                    };
                    config.QueryParameters.Orderby = new[] { "displayName asc" };
                });

                if (response?.Value != null)
                {
                    apps.AddRange(response.Value);
                }

                return apps;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting applications: {ex.Message}", ex);
            }
        }

        public async Task<List<ApplicationBackup>> LoadBackupAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                await LogAsync($"Loading backup from: {filePath}");
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    ReferenceHandler = ReferenceHandler.Preserve,
                    Converters = 
                    {
                        new JsonStringEnumConverter()
                    }
                };
                
                var backups = JsonSerializer.Deserialize<List<ApplicationBackup>>(json, options);
                
                // Debug logging
                foreach (var backup in backups ?? new List<ApplicationBackup>())
                {
                    await LogAsync($"Loaded backup for app: {backup.Application?.DisplayName}");
                    
                    // Log Service Principal details
                    if (backup.ServicePrincipal != null)
                    {
                        await LogAsync($"Found Service Principal: {backup.ServicePrincipal.DisplayName}");
                        await LogAsync($"Service Principal Type: {backup.ServicePrincipal.ServicePrincipalType}");
                        await LogAsync($"Tags: {string.Join(", ", backup.ServicePrincipal.Tags ?? new List<string>())}");
                        await LogAsync($"Login URL: {backup.ServicePrincipal.LoginUrl}");
                        await LogAsync($"Key Credentials Count: {backup.ServicePrincipal.KeyCredentials?.Count ?? 0}");

                        // Ensure all collections are initialized
                        backup.ServicePrincipal.Tags ??= new List<string>();
                        backup.ServicePrincipal.KeyCredentials ??= new List<KeyCredential>();
                        backup.ServicePrincipal.PasswordCredentials ??= new List<PasswordCredential>();
                        backup.ServicePrincipal.NotificationEmailAddresses ??= new List<string>();
                    }
                    else
                    {
                        await LogAsync("WARNING: No Service Principal found in backup!");
                        // Log the raw JSON for debugging
                        await LogAsync("Raw JSON for this backup item:");
                        var rawJson = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
                        await LogAsync(rawJson);
                    }

                    // Log Application details
                    await LogAsync($"IdentifierUris count: {backup.Application?.IdentifierUris?.Count ?? 0}");
                    if (backup.Application?.IdentifierUris?.Any() == true)
                    {
                        foreach (var uri in backup.Application.IdentifierUris)
                        {
                            await LogAsync($"Found IdentifierUri: {uri}");
                        }
                    }
                    else
                    {
                        await LogAsync("No IdentifierUris found in backup");
                    }
                }
                
                return backups ?? new List<ApplicationBackup>();
            }
            catch (Exception ex)
            {
                await LogAsync($"Error loading backup: {ex.Message}");
                await LogAsync($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    await LogAsync($"Inner exception: {ex.InnerException.Message}");
                    await LogAsync($"Inner stack trace: {ex.InnerException.StackTrace}");
                }
                throw new Exception($"Error loading backup: {ex.Message}", ex);
            }
        }

        private async Task<GraphApplication> GetApplicationByAppIdAsync(string appId)
        {
            try
            {
                var response = await _graphClient.Applications.GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"appId eq '{appId}'";
                    config.QueryParameters.Select = new string[]
                    {
                        "id",
                        "appId",
                        "displayName",
                        "identifierUris",
                        "api",
                        "appRoles",
                        "info",
                        "keyCredentials",
                        "passwordCredentials",
                        "publicClient",
                        "requiredResourceAccess",
                        "signInAudience",
                        "spa",
                        "tags",
                        "web"
                    };
                });

                return response?.Value?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting application by AppId: {ex.Message}", ex);
            }
        }

        private async Task<ServicePrincipal> GetServicePrincipalAsync(string appId)
        {
            try
            {
                var response = await _graphClient.ServicePrincipals.GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"appId eq '{appId}'";
                    config.QueryParameters.Select = new string[] 
                    { 
                        "id",
                        "appId",
                        "displayName",
                        "appRoleAssignmentRequired",
                        "loginUrl",
                        "logoutUrl",
                        "preferredTokenSigningKeyThumbprint",
                        "samlSingleSignOnSettings",
                        "servicePrincipalType",
                        "tags",
                        "keyCredentials",
                        "passwordCredentials"
                    };
                });

                return response?.Value?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting service principal: {ex.Message}", ex);
            }
        }

        public async Task SaveBackupAsync(List<GraphApplication> apps, string filePath)
        {
            try
            {
                var backups = new List<ApplicationBackup>();
                foreach (var app in apps)
                {
                    await LogAsync($"Backing up application: {app.DisplayName}");
                    
                    var backup = new ApplicationBackup
                    {
                        Application = app,
                        BackupDate = DateTime.UtcNow
                    };

                    try
                    {
                        // Get full application details including credentials
                        var fullApp = await GetApplicationByAppIdAsync(app.AppId);
                        if (fullApp != null)
                        {
                            backup.Application = fullApp;
                        }

                        var servicePrincipal = await GetServicePrincipalAsync(app.AppId);
                        if (servicePrincipal != null)
                        {
                            // Create a clean ServicePrincipal object without backing store
                            backup.ServicePrincipal = new ServicePrincipal
                            {
                                AppId = servicePrincipal.AppId,
                                Id = servicePrincipal.Id,
                                DisplayName = servicePrincipal.DisplayName,
                                ServicePrincipalType = servicePrincipal.ServicePrincipalType,
                                LoginUrl = servicePrincipal.LoginUrl,
                                PreferredTokenSigningKeyThumbprint = servicePrincipal.PreferredTokenSigningKeyThumbprint,
                                Tags = servicePrincipal.Tags?.ToList() ?? new List<string>(),
                                NotificationEmailAddresses = servicePrincipal.NotificationEmailAddresses?.ToList() ?? new List<string>(),
                                KeyCredentials = servicePrincipal.KeyCredentials?.ToList() ?? new List<KeyCredential>(),
                                PasswordCredentials = servicePrincipal.PasswordCredentials?.ToList() ?? new List<PasswordCredential>(),
                                SamlSingleSignOnSettings = servicePrincipal.SamlSingleSignOnSettings,
                                AppRoleAssignmentRequired = servicePrincipal.AppRoleAssignmentRequired ?? false
                            };
                            
                            // For SAML apps, log additional details
                            if (servicePrincipal.Tags?.Contains("WindowsAzureActiveDirectoryCustomSingleSignOnApplication") == true)
                            {
                                await LogAsync($"Found SAML application: {app.DisplayName}");
                                await LogAsync($"Login URL: {servicePrincipal.LoginUrl}");
                                await LogAsync($"Token Signing Key Thumbprint: {servicePrincipal.PreferredTokenSigningKeyThumbprint}");
                                
                                if (servicePrincipal.KeyCredentials?.Any() == true)
                                {
                                    await LogAsync($"Found {servicePrincipal.KeyCredentials.Count} certificates");
                                    foreach (var cert in servicePrincipal.KeyCredentials)
                                    {
                                        await LogAsync($"Certificate: {cert.DisplayName}, Type: {cert.Type}, Usage: {cert.Usage}");
                                        await LogAsync($"Valid from {cert.StartDateTime} to {cert.EndDateTime}");
                                    }
                                }

                                if (servicePrincipal.SamlSingleSignOnSettings != null)
                                {
                                    await LogAsync("Found SAML SSO settings");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await LogAsync($"Error backing up app {app.DisplayName}: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Error backing up app {app.DisplayName}: {ex.Message}");
                    }

                    backups.Add(backup);
                }

                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                };
                
                var json = JsonSerializer.Serialize(backups, options);
                await File.WriteAllTextAsync(filePath, json);
                await LogAsync($"Backup completed successfully. Saved to: {filePath}");
            }
            catch (Exception ex)
            {
                await LogAsync($"Error saving backup: {ex.Message}");
                throw new Exception($"Error saving backup: {ex.Message}", ex);
            }
        }

        public async Task RestoreApplicationAsync(ApplicationBackup backup)
        {
            try
            {
                if (backup == null)
                {
                    throw new ArgumentNullException(nameof(backup), "Backup cannot be null");
                }

                // Log the state of the backup object
                await LogAsync("Validating backup data...");
                await LogAsync($"Application is null: {backup.Application == null}");
                await LogAsync($"ServicePrincipal is null: {backup.ServicePrincipal == null}");
                if (backup.ServicePrincipal != null)
                {
                    await LogAsync($"Service Principal details before restore:");
                    await LogAsync($"DisplayName: {backup.ServicePrincipal.DisplayName}");
                    await LogAsync($"AppId: {backup.ServicePrincipal.AppId}");
                    await LogAsync($"ServicePrincipalType: {backup.ServicePrincipal.ServicePrincipalType}");
                }

                if (backup.ServicePrincipal == null)
                {
                    // Log the entire backup object for debugging
                    await LogAsync("DEBUG: Dumping full backup object:");
                    var backupJson = JsonSerializer.Serialize(backup, new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    });
                    await LogAsync(backupJson);
                    throw new ArgumentException("Backup does not contain a service principal", nameof(backup));
                }

                var sp = backup.ServicePrincipal;
                await LogAsync($"Starting restore process for application: {sp.DisplayName}");

                // Create service principal using the non-gallery application template
                await LogAsync("Creating Enterprise Application using SAML SSO template");
                var newSp = await _graphClient.ServicePrincipals.PostAsync(new ServicePrincipal
                {
                    AppId = "8adf8e6e-67b2-4cf2-a259-e3dc5476c621", // Non-gallery application template ID
                    ServicePrincipalType = "Application",
                    AccountEnabled = true,
                    DisplayName = sp.DisplayName,
                    PreferredSingleSignOnMode = "saml",
                    LoginUrl = sp.LoginUrl,
                    Tags = new List<string> 
                    { 
                        "WindowsAzureActiveDirectoryCustomSingleSignOnApplication",
                        "WindowsAzureActiveDirectoryIntegratedApp"
                    },
                    NotificationEmailAddresses = sp.NotificationEmailAddresses ?? new List<string>(),
                    PreferredTokenSigningKeyThumbprint = sp.PreferredTokenSigningKeyThumbprint,
                    ReplyUrls = backup.Application?.Web?.RedirectUris?.ToList() ?? new List<string>(),
                    AppRoleAssignmentRequired = sp.AppRoleAssignmentRequired
                });

                await LogAsync($"Successfully created service principal: {newSp.DisplayName}");

                // Restore certificates if any
                if (sp.KeyCredentials?.Any() == true)
                {
                    await LogAsync($"Restoring {sp.KeyCredentials.Count} certificates");
                    foreach (var cert in sp.KeyCredentials)
                    {
                        try
                        {
                            var addKeyPostRequestBody = new AddKeyPostRequestBody
                            {
                                KeyCredential = cert,
                                Proof = "proof"
                            };

                            await _graphClient.ServicePrincipals[newSp.Id].AddKey.PostAsync(addKeyPostRequestBody);
                            await LogAsync($"Restored certificate: {cert.DisplayName}");
                        }
                        catch (Exception ex)
                        {
                            await LogAsync($"Error restoring certificate {cert.DisplayName}: {ex.Message}");
                        }
                    }
                }

                // Restore secrets if any
                if (sp.PasswordCredentials?.Any() == true)
                {
                    await LogAsync($"Restoring {sp.PasswordCredentials.Count} secrets");
                    foreach (var secret in sp.PasswordCredentials)
                    {
                        try
                        {
                            var addPasswordPostRequestBody = new AddPasswordPostRequestBody
                            {
                                PasswordCredential = secret
                            };

                            await _graphClient.ServicePrincipals[newSp.Id].AddPassword.PostAsync(addPasswordPostRequestBody);
                            await LogAsync($"Restored secret: {secret.DisplayName}");
                        }
                        catch (Exception ex)
                        {
                            await LogAsync($"Error restoring secret {secret.DisplayName}: {ex.Message}");
                        }
                    }
                }

                await LogAsync($"Successfully restored application: {sp.DisplayName}");
            }
            catch (Exception ex)
            {
                await LogAsync($"Error restoring application {backup?.ServicePrincipal?.DisplayName}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    await LogAsync($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        private async Task UpdateApplicationAsync(string id, GraphApplication app)
        {
            try
            {
                await _graphClient.Applications[id].PatchAsync(app);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating application: {ex.Message}", ex);
            }
        }

        private async Task CreateApplicationAsync(GraphApplication app)
        {
            try
            {
                await _graphClient.Applications.PostAsync(app);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating application: {ex.Message}", ex);
            }
        }

        private async Task<List<PasswordCredential>> GetSecretsAsync(string servicePrincipalId)
        {
            try
            {
                var response = await _graphClient.ServicePrincipals[servicePrincipalId].AddPassword.PostAsync(new AddPasswordPostRequestBody());
                return response != null ? new List<PasswordCredential> { response } : new List<PasswordCredential>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting secrets: {ex.Message}");
                return new List<PasswordCredential>();
            }
        }

        private async Task<List<KeyCredential>> GetCertificatesAsync(string servicePrincipalId)
        {
            try
            {
                var servicePrincipal = await _graphClient.ServicePrincipals[servicePrincipalId].GetAsync(config =>
                {
                    config.QueryParameters.Select = new string[] { "keyCredentials" };
                });

                return servicePrincipal?.KeyCredentials?.ToList() ?? new List<KeyCredential>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting certificates: {ex.Message}");
                return new List<KeyCredential>();
            }
        }

        private async Task<SynchronizationJob> GetSyncJobAsync(string servicePrincipalId)
        {
            try
            {
                var response = await _graphClient.ServicePrincipals[servicePrincipalId].Synchronization.Jobs.GetAsync();
                return response?.Value?.FirstOrDefault();
            }
            catch
            {
                return null; // Some apps might not have sync jobs
            }
        }

        private async Task<SynchronizationTemplate> GetSyncTemplateAsync(string servicePrincipalId)
        {
            try
            {
                var response = await _graphClient.ServicePrincipals[servicePrincipalId].Synchronization.Templates.GetAsync();
                return response?.Value?.FirstOrDefault();
            }
            catch
            {
                return null; // Some apps might not have sync templates
            }
        }

        public string FormatResourceAccess(IList<RequiredResourceAccess> access)
        {
            if (access == null || !access.Any())
                return string.Empty;

            return string.Join(", ", access.Select(r => r.ResourceAppId));
        }

        public string FormatApiSettings(ApiApplication api)
        {
            if (api == null)
                return string.Empty;

            var settings = new List<string>();
            if (api.Oauth2PermissionScopes?.Any() == true)
                settings.Add($"Scopes: {api.Oauth2PermissionScopes.Count}");
            if (api.PreAuthorizedApplications?.Any() == true)
                settings.Add($"Pre-authorized apps: {api.PreAuthorizedApplications.Count}");
            if (api.RequestedAccessTokenVersion.HasValue)
                settings.Add($"Token version: {api.RequestedAccessTokenVersion}");

            return string.Join(", ", settings);
        }

        public string FormatAppRoles(IList<AppRole> roles)
        {
            if (roles == null || !roles.Any())
                return string.Empty;

            return string.Join(", ", roles.Select(r => r.DisplayName ?? r.Id.ToString()));
        }

        public string FormatInfo(InformationalUrl info)
        {
            if (info == null)
                return string.Empty;

            var urls = new List<string>();
            if (!string.IsNullOrEmpty(info.MarketingUrl))
                urls.Add($"Marketing: {info.MarketingUrl}");
            if (!string.IsNullOrEmpty(info.PrivacyStatementUrl))
                urls.Add($"Privacy: {info.PrivacyStatementUrl}");
            if (!string.IsNullOrEmpty(info.SupportUrl))
                urls.Add($"Support: {info.SupportUrl}");
            if (!string.IsNullOrEmpty(info.TermsOfServiceUrl))
                urls.Add($"Terms: {info.TermsOfServiceUrl}");

            return string.Join(", ", urls);
        }

        public IList<RequiredResourceAccess> ParseResourceAccess(string value)
        {
            try
            {
                return !string.IsNullOrEmpty(value) 
                    ? JsonSerializer.Deserialize<List<RequiredResourceAccess>>(value) 
                    : new List<RequiredResourceAccess>();
            }
            catch
            {
                return new List<RequiredResourceAccess>();
            }
        }

        public ApiApplication ParseApiSettings(string value)
        {
            try
            {
                return !string.IsNullOrEmpty(value)
                    ? JsonSerializer.Deserialize<ApiApplication>(value)
                    : new ApiApplication();
            }
            catch
            {
                return new ApiApplication();
            }
        }

        public IList<AppRole> ParseAppRoles(string value)
        {
            try
            {
                return !string.IsNullOrEmpty(value)
                    ? JsonSerializer.Deserialize<List<AppRole>>(value)
                    : new List<AppRole>();
            }
            catch
            {
                return new List<AppRole>();
            }
        }

        public InformationalUrl ParseInfo(string value)
        {
            try
            {
                return !string.IsNullOrEmpty(value)
                    ? JsonSerializer.Deserialize<InformationalUrl>(value)
                    : new InformationalUrl();
            }
            catch
            {
                return new InformationalUrl();
            }
        }
    }
}