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

        public async Task SaveBackupAsync(List<GraphApplication> apps, string filePath)
        {
            try
            {
                var backups = new List<ApplicationBackup>();
                foreach (var app in apps)
                {
                    var backup = new ApplicationBackup
                    {
                        Application = app,
                        BackupDate = DateTime.UtcNow
                    };

                    try
                    {
                        var servicePrincipal = await GetServicePrincipalAsync(app.AppId);
                        if (servicePrincipal != null)
                        {
                            backup.ServicePrincipal = servicePrincipal;
                            backup.Secrets = await GetSecretsAsync(servicePrincipal.Id);
                            backup.Certificates = await GetCertificatesAsync(servicePrincipal.Id);
                            
                            // Get SCIM provisioning configuration
                            backup.SyncJob = await GetSyncJobAsync(servicePrincipal.Id);
                            backup.SyncTemplate = await GetSyncTemplateAsync(servicePrincipal.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with the backup
                        System.Diagnostics.Debug.WriteLine($"Error backing up app {app.DisplayName}: {ex.Message}");
                    }

                    backups.Add(backup);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(backups, options);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving backup: {ex.Message}", ex);
            }
        }

        public async Task RestoreApplicationAsync(ApplicationBackup backup)
        {
            try
            {
                await LogAsync($"Starting restore process for application: {backup.Application.DisplayName}");

                // Step 1: Create the basic application registration
                var app = backup.Application;
                await LogAsync("Step 1: Creating basic application registration");
                var newApp = await _graphClient.Applications.PostAsync(new GraphApplication
                {
                    DisplayName = app.DisplayName,
                    Description = app.Description,
                    Notes = app.Notes,
                    SignInAudience = app.SignInAudience ?? "AzureADMyOrg"
                });

                if (newApp == null)
                {
                    await LogAsync("ERROR: Failed to create application");
                    throw new Exception($"Failed to create application {app.DisplayName}");
                }

                await LogAsync($"Created application with ID: {newApp.Id} and AppId: {newApp.AppId}");
                await Task.Delay(5000); // Increased delay

                // Step 2: Update the application with additional properties
                await LogAsync("Step 2: Updating application with additional properties");
                var updateApp = new GraphApplication
                {
                    Api = app.Api != null ? new ApiApplication
                    {
                        AcceptMappedClaims = app.Api.AcceptMappedClaims ?? false,
                        RequestedAccessTokenVersion = app.Api.RequestedAccessTokenVersion ?? 2,
                        Oauth2PermissionScopes = app.Api.Oauth2PermissionScopes?.Select(s => new PermissionScope
                        {
                            Id = s.Id,
                            AdminConsentDescription = s.AdminConsentDescription,
                            AdminConsentDisplayName = s.AdminConsentDisplayName,
                            IsEnabled = s.IsEnabled ?? true,
                            Type = s.Type,
                            UserConsentDescription = s.UserConsentDescription,
                            UserConsentDisplayName = s.UserConsentDisplayName,
                            Value = s.Value
                        }).ToList() ?? new List<PermissionScope>(),
                        PreAuthorizedApplications = app.Api.PreAuthorizedApplications?.Select(p => new PreAuthorizedApplication
                        {
                            AppId = p.AppId,
                            DelegatedPermissionIds = p.DelegatedPermissionIds?.ToList() ?? new List<string>()
                        }).ToList() ?? new List<PreAuthorizedApplication>()
                    } : null,
                    Web = app.Web != null ? new WebApplication
                    {
                        HomePageUrl = app.Web.HomePageUrl,
                        LogoutUrl = app.Web.LogoutUrl,
                        RedirectUris = app.Web.RedirectUris?.ToList() ?? new List<string>()
                    } : null,
                    Spa = app.Spa != null ? new SpaApplication
                    {
                        RedirectUris = app.Spa.RedirectUris?.ToList() ?? new List<string>()
                    } : null,
                    PublicClient = app.PublicClient != null ? new PublicClientApplication
                    {
                        RedirectUris = app.PublicClient.RedirectUris?.ToList() ?? new List<string>()
                    } : null
                };

                await _graphClient.Applications[newApp.Id].PatchAsync(updateApp);
                await LogAsync("Updated application with additional properties");
                await Task.Delay(5000); // Increased delay

                // Step 3: Create the enterprise application (service principal)
                await LogAsync("Step 3: Creating enterprise application (service principal)");
                
                try
                {
                    var sp = backup.ServicePrincipal;
                    var newSp = await _graphClient.ServicePrincipals.PostAsync(new ServicePrincipal
                    {
                        AppId = newApp.AppId,
                        AccountEnabled = true,
                        DisplayName = sp?.DisplayName ?? app.DisplayName,
                        Description = sp?.Description ?? app.Description,
                        Notes = sp?.Notes,
                        ServicePrincipalType = "Application",
                        PreferredSingleSignOnMode = sp?.PreferredSingleSignOnMode ?? "saml",
                        AppRoleAssignmentRequired = sp?.AppRoleAssignmentRequired ?? false,
                        Homepage = sp?.Homepage ?? app.Web?.HomePageUrl,
                        LogoutUrl = sp?.LogoutUrl ?? app.Web?.LogoutUrl,
                        ReplyUrls = (sp?.ReplyUrls?.ToList() ?? app.Web?.RedirectUris?.ToList() ?? new List<string>()),
                        Tags = sp?.Tags?.ToList() ?? app.Tags?.ToList() ?? new List<string>()
                    });

                    if (newSp != null)
                    {
                        await LogAsync($"Created service principal with ID: {newSp.Id}");
                        await Task.Delay(5000); // Increased delay

                        // Step 4: Restore secrets
                        if (backup.Secrets?.Any() == true)
                        {
                            await LogAsync("Step 4: Restoring secrets");
                            foreach (var secret in backup.Secrets)
                            {
                                await _graphClient.ServicePrincipals[newSp.Id].AddPassword.PostAsync(new AddPasswordPostRequestBody
                                {
                                    PasswordCredential = new PasswordCredential
                                    {
                                        DisplayName = secret.DisplayName,
                                        EndDateTime = secret.EndDateTime
                                    }
                                });
                                await LogAsync($"Added secret: {secret.DisplayName}");
                                await Task.Delay(2000);
                            }
                        }

                        // Step 5: Restore certificates
                        if (backup.Certificates?.Any() == true)
                        {
                            await LogAsync("Step 5: Restoring certificates");
                            foreach (var cert in backup.Certificates)
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
                                await LogAsync($"Added certificate: {cert.DisplayName}");
                                await Task.Delay(2000);
                            }
                        }

                        // Step 6: Restore SCIM provisioning
                        if (backup.SyncTemplate != null)
                        {
                            await LogAsync("Step 6: Restoring SCIM provisioning");
                            await _graphClient.ServicePrincipals[newSp.Id].Synchronization.Templates.PostAsync(backup.SyncTemplate);
                            await Task.Delay(2000);
                        }

                        if (backup.SyncJob != null)
                        {
                            await _graphClient.ServicePrincipals[newSp.Id].Synchronization.Jobs.PostAsync(backup.SyncJob);
                            await LogAsync("Added SCIM sync job");
                        }
                    }
                    else
                    {
                        await LogAsync("ERROR: Failed to create service principal");
                    }
                }
                catch (Exception spEx)
                {
                    await LogAsync($"ERROR creating service principal: {spEx.Message}");
                    await LogAsync($"Stack trace: {spEx.StackTrace}");
                    throw;
                }

                await LogAsync($"Completed restore process for application: {app.DisplayName}");
            }
            catch (Exception ex)
            {
                await LogAsync($"ERROR in restore process: {ex.Message}");
                await LogAsync($"Stack trace: {ex.StackTrace}");
                throw new Exception($"Error restoring application {backup.Application.DisplayName}: {ex.Message}", ex);
            }
        }

        private async Task<GraphApplication> GetApplicationByAppIdAsync(string appId)
        {
            try
            {
                var response = await _graphClient.Applications.GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"appId eq '{appId}'";
                });

                return response?.Value?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting application by AppId: {ex.Message}", ex);
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

        private async Task<ServicePrincipal> GetServicePrincipalAsync(string appId)
        {
            try
            {
                var response = await _graphClient.ServicePrincipals.GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"appId eq '{appId}'";
                });

                return response?.Value?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting service principal: {ex.Message}", ex);
            }
        }

        private async Task<List<PasswordCredential>> GetSecretsAsync(string servicePrincipalId)
        {
            try
            {
                var response = await _graphClient.ServicePrincipals[servicePrincipalId].GetAsync(config =>
                {
                    config.QueryParameters.Select = new[] { "passwordCredentials" };
                });

                return response?.PasswordCredentials?.ToList() ?? new List<PasswordCredential>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting secrets: {ex.Message}", ex);
            }
        }

        private async Task<List<KeyCredential>> GetCertificatesAsync(string servicePrincipalId)
        {
            try
            {
                var response = await _graphClient.ServicePrincipals[servicePrincipalId].GetAsync(config =>
                {
                    config.QueryParameters.Select = new[] { "keyCredentials" };
                });

                return response?.KeyCredentials?.ToList() ?? new List<KeyCredential>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting certificates: {ex.Message}", ex);
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