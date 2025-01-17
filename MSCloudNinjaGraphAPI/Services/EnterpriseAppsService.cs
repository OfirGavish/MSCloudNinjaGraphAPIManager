using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.ServicePrincipals.Item.AddPassword;
using Microsoft.Graph.ServicePrincipals.Item.AddKey;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.IO;
using GraphApplication = Microsoft.Graph.Models.Application;

namespace MSCloudNinjaGraphAPI.Services
{
    public class ApplicationBackup
    {
        public GraphApplication Application { get; set; }
        public DateTime BackupDate { get; set; }
        public ServicePrincipal ServicePrincipal { get; set; }
        public List<PasswordCredential> Secrets { get; set; }
        public List<KeyCredential> Certificates { get; set; }
        public SynchronizationJob SyncJob { get; set; }
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
                var backups = JsonSerializer.Deserialize<List<ApplicationBackup>>(json);
                return backups ?? new List<ApplicationBackup>();
            }
            catch (Exception ex)
            {
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
                            backup.ServicePrincipal = servicePrincipal;
                            
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
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
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
                await LogAsync($"Starting restore process for application: {backup.Application.DisplayName}");
                await LogAsync("Step 1: Creating Application Registration");

                var app = backup.Application;
                var newApp = new GraphApplication
                {
                    DisplayName = app.DisplayName,
                    SignInAudience = app.SignInAudience,
                    Api = app.Api,
                    AppRoles = app.AppRoles,
                    IdentifierUris = app.IdentifierUris,
                    Info = app.Info,
                    RequiredResourceAccess = app.RequiredResourceAccess,
                    Web = app.Web,
                    Tags = app.Tags
                };

                await _graphClient.Applications.PostAsync(newApp);
                await LogAsync($"Created application registration: {newApp.DisplayName}");
                await Task.Delay(5000); // Wait for app registration to propagate

                // Step 2: Get the new application to get its ID
                var createdApp = await GetApplicationByAppIdAsync(newApp.AppId);
                if (createdApp == null)
                {
                    throw new Exception("Could not find newly created application");
                }

                await LogAsync("Step 2: Creating Service Principal");
                var sp = backup.ServicePrincipal;
                var newSp = await _graphClient.ServicePrincipals.PostAsync(new ServicePrincipal
                {
                    AppId = newApp.AppId,
                    AppRoleAssignmentRequired = sp.AppRoleAssignmentRequired,
                    DisplayName = sp.DisplayName,
                    LoginUrl = sp.LoginUrl,
                    ServicePrincipalType = sp.ServicePrincipalType,
                    Tags = sp.Tags,
                    PreferredTokenSigningKeyThumbprint = sp.PreferredTokenSigningKeyThumbprint
                });

                if (newSp != null)
                {
                    await LogAsync($"Created service principal with ID: {newSp.Id}");
                    await Task.Delay(5000);

                    // Step 3: Update App Registration's web configuration
                    await LogAsync("Step 3: Updating App Registration web configuration");
                    if (app.Web?.RedirectUris?.Any() == true)
                    {
                        var updateApp = new GraphApplication
                        {
                            Web = app.Web,
                            IdentifierUris = app.IdentifierUris
                        };
                        await _graphClient.Applications[createdApp.Id].PatchAsync(updateApp);
                        await LogAsync($"Updated App Registration with {app.Web.RedirectUris.Count} Redirect URIs");
                        foreach (var url in app.Web.RedirectUris)
                        {
                            await LogAsync($"Added Redirect URI: {url}");
                        }
                        await Task.Delay(2000);
                    }

                    // Step 4: Add certificates
                    if (sp.KeyCredentials?.Any() == true)
                    {
                        await LogAsync("Step 4: Adding certificates");
                        foreach (var cert in sp.KeyCredentials)
                        {
                            try
                            {
                                var keyCredential = new KeyCredential
                                {
                                    Type = cert.Type,
                                    Usage = cert.Usage,
                                    Key = cert.Key,
                                    DisplayName = cert.DisplayName,
                                    StartDateTime = cert.StartDateTime,
                                    EndDateTime = cert.EndDateTime,
                                    CustomKeyIdentifier = cert.CustomKeyIdentifier
                                };

                                await _graphClient.ServicePrincipals[newSp.Id].AddKey.PostAsync(new AddKeyPostRequestBody
                                {
                                    KeyCredential = keyCredential,
                                    Proof = "proof"
                                });
                                await LogAsync($"Added certificate: {keyCredential.DisplayName}");
                                await LogAsync($"Certificate type: {keyCredential.Type}, usage: {keyCredential.Usage}");
                                await LogAsync($"Valid from {keyCredential.StartDateTime} to {keyCredential.EndDateTime}");
                                await Task.Delay(2000);
                            }
                            catch (Exception certEx)
                            {
                                await LogAsync($"Error adding certificate {cert.DisplayName}: {certEx.Message}");
                            }
                        }
                    }

                    // Step 5: Add password credentials if any
                    if (sp.PasswordCredentials?.Any() == true)
                    {
                        await LogAsync("Step 5: Adding password credentials");
                        foreach (var cred in sp.PasswordCredentials)
                        {
                            try
                            {
                                var passwordCredential = new PasswordCredential
                                {
                                    DisplayName = cred.DisplayName,
                                    StartDateTime = cred.StartDateTime,
                                    EndDateTime = cred.EndDateTime,
                                    CustomKeyIdentifier = cred.CustomKeyIdentifier
                                };

                                await _graphClient.ServicePrincipals[newSp.Id].AddPassword.PostAsync(new AddPasswordPostRequestBody
                                {
                                    PasswordCredential = passwordCredential
                                });
                                await LogAsync($"Added password credential: {passwordCredential.DisplayName}");
                                await Task.Delay(2000);
                            }
                            catch (Exception credEx)
                            {
                                await LogAsync($"Error adding password credential {cred.DisplayName}: {credEx.Message}");
                            }
                        }
                    }

                    // Step 6: Update SAML SSO settings
                    if (sp.Tags?.Contains("WindowsAzureActiveDirectoryCustomSingleSignOnApplication") == true)
                    {
                        await LogAsync("Step 6: Updating SAML SSO settings");
                        var samlSettings = new ServicePrincipal
                        {
                            PreferredTokenSigningKeyThumbprint = sp.PreferredTokenSigningKeyThumbprint,
                            LoginUrl = sp.LoginUrl,
                            SamlSingleSignOnSettings = sp.SamlSingleSignOnSettings
                        };
                        await _graphClient.ServicePrincipals[newSp.Id].PatchAsync(samlSettings);
                        await LogAsync("Updated SAML SSO settings");
                        await Task.Delay(2000);
                    }

                    await LogAsync($"Successfully restored application: {app.DisplayName}");
                }
            }
            catch (Exception ex)
            {
                await LogAsync($"Error restoring application {backup.Application.DisplayName}: {ex.Message}");
                throw new Exception($"Error restoring application {backup.Application.DisplayName}: {ex.Message}", ex);
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