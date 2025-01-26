using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Authentication.Azure;
using Microsoft.Kiota.Abstractions.Authentication;
using Azure.Identity;
using MSCloudNinjaGraphAPI.Services.Interfaces;
using MSCloudNinjaGraphAPI.Models;
using Microsoft.Graph.ServicePrincipals.Item.AddPassword;
using Microsoft.Graph.ServicePrincipals.Item.AddKey;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Http.HttpClientLibrary;
using System.Net.Http.Headers;
using System.Net.Http;
using GraphApplication = Microsoft.Graph.Models.Application;

namespace MSCloudNinjaGraphAPI.Services
{
    public class EnterpriseAppsService : IEnterpriseAppsService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly LogService _logService;
        private readonly SsoSettingsService _ssoSettingsService;
        private readonly IBackupComponent _backupComponent;

        public EnterpriseAppsService(GraphServiceClient graphClient)
        {
            _graphClient = graphClient;
            _logService = new LogService();
            _backupComponent = new BackupComponent();
            _ssoSettingsService = new SsoSettingsService(_backupComponent);
        }

        private async Task LogAsync(string message, bool isError = false)
        {
            await _logService.LogAsync(message, isError);
        }

        private async Task<string> GetGraphTokenAsync()
        {
            try
            {
                // Try to get the token by making a direct request to Graph API
                var response = await _graphClient.Users.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Top = 1;
                    requestConfiguration.QueryParameters.Select = new[] { "id" };
                });

                // If we got here, we can access the adapter
                if (_graphClient.RequestAdapter is HttpClientRequestAdapter adapter)
                {
                    var handler = adapter.GetType().GetField("pipeline", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(adapter) as HttpMessageHandler;
                    var httpClient = new HttpClient(handler);
                    
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/users?$top=1");
                    var result = await httpClient.SendAsync(request);
                    
                    if (result.IsSuccessStatusCode)
                    {
                        var authHeader = result.RequestMessage?.Headers.Authorization;
                        if (authHeader != null)
                        {
                            return authHeader.Parameter;
                        }
                    }
                    await LogAsync($"Request failed with status: {result.StatusCode}", true);
                }
                else
                {
                    await LogAsync("RequestAdapter is not of type HttpClientRequestAdapter", true);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                await LogAsync($"Failed to get Graph token: {ex.GetType().Name} - {ex.Message}", true);
                if (ex.InnerException != null)
                {
                    await LogAsync($"Inner exception: {ex.InnerException.Message}", true);
                }
                return null;
            }
        }

        public async Task<List<GraphApplication>> GetApplicationsAsync()
        {
            var apps = await _graphClient.Applications.GetAsync();
            return apps?.Value?.ToList() ?? new List<GraphApplication>();
        }

        public async Task<GraphApplication> GetApplicationByAppIdAsync(string appId)
        {
            var apps = await _graphClient.Applications.GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Filter = $"appId eq '{appId}'";
            });

            return apps?.Value?.FirstOrDefault();
        }

        public async Task SaveBackupAsync(List<GraphApplication> apps, string filePath)
        {
            try
            {
                await LogAsync($"Starting backup of {apps.Count} applications to {filePath}");
                var backups = new List<ApplicationBackup>();
                foreach (var app in apps)
                {
                    await LogAsync($"Backing up application: {app.DisplayName}");

                    var backup = new ApplicationBackup
                    {
                        Application = app,
                        BackupDate = DateTime.UtcNow,
                        UserAssignments = new List<ServicePrincipalUserAssignment>(),
                        GroupAssignments = new List<ServicePrincipalGroupAssignment>(),
                        AppRoleAssignments = new List<AppRoleAssignment>()
                    };

                    try
                    {
                        // Get full application details including credentials
                        var fullApp = await GetApplicationByAppIdAsync(app.AppId);
                        if (fullApp != null)
                        {
                            backup.Application = fullApp;
                            await LogAsync($"Retrieved full application details for {fullApp.DisplayName}");
                        }

                        var servicePrincipal = await GetServicePrincipalAsync(app.AppId);
                        if (servicePrincipal != null)
                        {
                            await LogAsync($"Found service principal for {app.DisplayName}");

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

                            // Backup SSO Configuration
                            try
                            {
                                var token = await GetGraphTokenAsync();
                                if (!string.IsNullOrEmpty(token))
                                {
                                    await LogAsync("Backing up SSO configuration...");
                                    var settingsId = await _ssoSettingsService.GetSsoSettingsId(app.AppId, token);
                                    if (!string.IsNullOrEmpty(settingsId))
                                    {
                                        backup.SsoConfiguration = await _ssoSettingsService.GetSsoConfiguration(app.AppId, settingsId, token);
                                        await LogAsync("Successfully backed up SSO configuration");
                                    }
                                }
                                else
                                {
                                    await LogAsync("Could not get Graph token for SSO configuration backup", true);
                                }
                            }
                            catch (Exception ex)
                            {
                                await LogAsync($"Error backing up SSO configuration: {ex.Message}", true);
                            }

                            await LogAsync("Backing up user assignments...");
                            try
                            {
                                var userAssignments = await _graphClient.ServicePrincipals[servicePrincipal.Id].AppRoleAssignedTo
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Select = new[] { "id", "principalId", "principalDisplayName", "appRoleId", "principalType" };
                                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                    });

                                if (userAssignments?.Value != null)
                                {
                                    foreach (var assignment in userAssignments.Value)
                                    {
                                        // Filter for user assignments
                                        if (assignment.PrincipalType?.ToString().Equals("User", StringComparison.OrdinalIgnoreCase) == true)
                                        {
                                            await LogAsync($"Found user assignment: {assignment.PrincipalDisplayName}");
                                            backup.UserAssignments.Add(new ServicePrincipalUserAssignment
                                            {
                                                UserId = assignment.PrincipalId?.ToString(),
                                                PrincipalDisplayName = assignment.PrincipalDisplayName,
                                                AppRoleId = assignment.AppRoleId?.ToString()
                                            });
                                        }
                                    }
                                    await LogAsync($"Total user assignments found: {backup.UserAssignments.Count}");
                                }
                            }
                            catch (Exception ex)
                            {
                                await LogAsync($"Error backing up user assignments: {ex.Message}", true);
                            }

                            await LogAsync("Backing up group assignments...");
                            try
                            {
                                var groupAssignments = await _graphClient.ServicePrincipals[servicePrincipal.Id].AppRoleAssignedTo
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Select = new[] { "id", "principalId", "principalDisplayName", "appRoleId", "principalType" };
                                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                    });

                                if (groupAssignments?.Value != null)
                                {
                                    foreach (var assignment in groupAssignments.Value)
                                    {
                                        // Filter for group assignments
                                        if (assignment.PrincipalType?.ToString().Equals("Group", StringComparison.OrdinalIgnoreCase) == true)
                                        {
                                            await LogAsync($"Found group assignment: {assignment.PrincipalDisplayName}");
                                            backup.GroupAssignments.Add(new ServicePrincipalGroupAssignment
                                            {
                                                GroupId = assignment.PrincipalId?.ToString(),
                                                GroupDisplayName = assignment.PrincipalDisplayName,
                                                AppRoleId = assignment.AppRoleId?.ToString()
                                            });
                                        }
                                    }
                                    await LogAsync($"Total group assignments found: {backup.GroupAssignments.Count}");
                                }
                            }
                            catch (Exception ex)
                            {
                                await LogAsync($"Error backing up group assignments: {ex.Message}", true);
                            }

                            await LogAsync("Backing up claims configuration...");
                            try
                            {
                                // Get standard claims mapping policies
                                await LogAsync("Fetching standard claims mapping policies...");
                                var standardPolicies = await _graphClient.Policies.ClaimsMappingPolicies
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Select = new[] { "id", "displayName", "definition" };
                                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                    });

                                if (standardPolicies?.Value?.Any() == true)
                                {
                                    await LogAsync($"Found {standardPolicies.Value.Count} standard claims mapping policies");
                                }

                                // Get assigned policies and SAML settings
                                await LogAsync("Fetching service principal details...");
                                var spDetails = await _graphClient.ServicePrincipals[servicePrincipal.Id]
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Select = new[] {
                                            "id",
                                            "displayName",
                                            "preferredSingleSignOnMode",
                                            "samlSingleSignOnSettings",
                                            "servicePrincipalType",
                                            "claimsMappingPolicies"
                                        };
                                    });

                                var isSamlApp = spDetails?.PreferredSingleSignOnMode?.Equals("saml", StringComparison.OrdinalIgnoreCase) == true;
                                await LogAsync($"Application SSO mode: {spDetails?.PreferredSingleSignOnMode}");

                                if (spDetails?.ClaimsMappingPolicies?.Any() == true)
                                {
                                    backup.ClaimsMapping = spDetails.ClaimsMappingPolicies.FirstOrDefault();
                                    await LogAsync($"Found claims mapping policy: {backup.ClaimsMapping.DisplayName}");
                                }

                                if (isSamlApp)
                                {
                                    await LogAsync("Application is SAML-based, fetching SAML configuration...");

                                    // Get application-level claims configuration including user claims
                                    var appClaims = await _graphClient.Applications[backup.Application.Id]
                                        .GetAsync(requestConfiguration =>
                                        {
                                            requestConfiguration.QueryParameters.Select = new[] {
                                                "web",
                                                "api",
                                                "optionalClaims",
                                                "groupMembershipClaims",
                                                "signInAudience",
                                                "tokenEncryptionKeyId",
                                                "tokenIssuancePolicies"
                                            };
                                            requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                        });

                                    backup.SamlConfiguration = new SamlConfiguration
                                    {
                                        SamlSingleSignOnSettings = spDetails.SamlSingleSignOnSettings,
                                        ClaimsMappings = spDetails.ClaimsMappingPolicies?.ToList(),
                                        OptionalClaims = appClaims?.OptionalClaims
                                    };

                                    if (backup.SamlConfiguration.SamlSingleSignOnSettings != null)
                                    {
                                        await LogAsync($"Found SAML SSO settings with relay state: {backup.SamlConfiguration.SamlSingleSignOnSettings.RelayState}");
                                    }

                                    try
                                    {
                                        // Get all claims configuration including user claims
                                        await LogAsync("Fetching application claims configuration...");
                                        var appWithClaims = await _graphClient.Applications[backup.Application.Id]
                                            .GetAsync(requestConfiguration =>
                                            {
                                                requestConfiguration.QueryParameters.Select = new[] {
                                                    "web",
                                                    "api",
                                                    "optionalClaims",
                                                    "groupMembershipClaims",
                                                    "signInAudience",
                                                    "tokenEncryptionKeyId",
                                                    "tokenIssuancePolicies"
                                                };
                                                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                            });

                                        var claimsData = new Dictionary<string, object>();

                                        // Add web claims
                                        if (appWithClaims?.Web?.AdditionalData != null)
                                        {
                                            foreach (var kvp in appWithClaims.Web.AdditionalData)
                                            {
                                                claimsData[$"web_{kvp.Key}"] = kvp.Value;
                                            }
                                            await LogAsync($"Found {appWithClaims.Web.AdditionalData.Count} web configuration properties");
                                        }

                                        // Get claims configuration from service principal
                                        var spClaims = await _graphClient.ServicePrincipals[servicePrincipal.Id]
                                            .GetAsync(config =>
                                            {
                                                config.Headers.Add("ConsistencyLevel", "eventual");
                                                config.QueryParameters.Select = new[] {
                                                    "appRoleAssignmentRequired",
                                                    "customSecurityAttributes",
                                                    "loginUrl",
                                                    "preferredSingleSignOnMode",
                                                    "samlSingleSignOnSettings",
                                                    "servicePrincipalNames",
                                                    "signInAudience",
                                                    "tokenEncryptionKeyId",
                                                    "verifiedPublisher"
                                                };
                                            });

                                        if (spClaims?.AdditionalData != null)
                                        {
                                            claimsData["servicePrincipalSettings"] = spClaims.AdditionalData;
                                            await LogAsync("Found service principal settings");
                                        }

                                        // Add user claims configuration
                                        if (appWithClaims?.OptionalClaims != null)
                                        {
                                            var userClaims = new
                                            {
                                                AccessToken = appWithClaims.OptionalClaims.AccessToken?
                                                    .Select(c => new { c.Name, c.Essential, c.AdditionalProperties, c.Source }).ToList(),
                                                IdToken = appWithClaims.OptionalClaims.IdToken?
                                                    .Select(c => new { c.Name, c.Essential, c.AdditionalProperties, c.Source }).ToList(),
                                                Saml2Token = appWithClaims.OptionalClaims.Saml2Token?
                                                    .Select(c => new { c.Name, c.Essential, c.AdditionalProperties, c.Source }).ToList()
                                            };
                                            claimsData["userClaims"] = userClaims;
                                            await LogAsync("Found user claims configuration");

                                            // Log claims details
                                            if (userClaims.Saml2Token?.Any() == true)
                                            {
                                                foreach (var claim in userClaims.Saml2Token)
                                                {
                                                    var details = new List<string>();
                                                    if (claim.Source != null) details.Add($"Source: {claim.Source}");
                                                    if (claim.AdditionalProperties?.Any() == true)
                                                        details.Add($"Properties: {string.Join(", ", claim.AdditionalProperties)}");

                                                    await LogAsync($"Found SAML2 claim: {claim.Name}" +
                                                        (details.Any() ? $" ({string.Join(", ", details)})" : ""));
                                                }
                                            }
                                            if (userClaims.AccessToken?.Any() == true)
                                            {
                                                foreach (var claim in userClaims.AccessToken)
                                                {
                                                    await LogAsync($"Found Access Token claim: {claim.Name}");
                                                    if (claim.Source != null)
                                                    {
                                                        await LogAsync($"  Source: {claim.Source}");
                                                    }
                                                }
                                            }
                                            if (userClaims.IdToken?.Any() == true)
                                            {
                                                foreach (var claim in userClaims.IdToken)
                                                {
                                                    await LogAsync($"Found ID Token claim: {claim.Name}");
                                                    if (claim.Source != null)
                                                    {
                                                        await LogAsync($"  Source: {claim.Source}");
                                                    }
                                                }
                                            }
                                        }

                                        // Add group membership claims if present
                                        if (appWithClaims.GroupMembershipClaims != null)
                                        {
                                            claimsData["groupMembershipClaims"] = appWithClaims.GroupMembershipClaims;
                                            await LogAsync($"Found group membership claims: {appWithClaims.GroupMembershipClaims}");
                                        }

                                        backup.SamlConfiguration.CustomAttributes = JsonSerializer.Serialize(claimsData);

                                        // Get claims mapping policies
                                        var policies = await _graphClient.ServicePrincipals[servicePrincipal.Id]
                                            .ClaimsMappingPolicies
                                            .GetAsync();
                                        if (policies?.Value?.Any() == true)
                                        {
                                            await LogAsync($"Found {policies.Value.Count} claims mapping policies");
                                            backup.SamlConfiguration.ClaimsMappings = policies.Value.ToList();
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        await LogAsync($"Error getting claims configuration: {ex.Message}", true);
                                    }
                                }
                                else
                                {
                                    await LogAsync("Application is not configured for SAML SSO");
                                }
                            }
                            catch (Exception ex)
                            {
                                await LogAsync($"Error backing up claims configuration: {ex.Message}", true);
                                if (ex.InnerException != null)
                                {
                                    await LogAsync($"Inner exception: {ex.InnerException.Message}", true);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await LogAsync($"Error backing up app {app.DisplayName}: {ex.Message}", true);
                        if (ex.InnerException != null)
                        {
                            await LogAsync($"Inner exception: {ex.InnerException.Message}", true);
                        }
                    }

                    backups.Add(backup);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    ReferenceHandler = ReferenceHandler.Preserve
                };

                var json = JsonSerializer.Serialize(backups, options);
                await File.WriteAllTextAsync(filePath, json);
                await LogAsync($"Backup completed successfully. Saved to: {filePath}");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error during backup: {ex.Message}";
                await LogAsync(errorMessage, true);
                throw new Exception(errorMessage, ex);
            }
        }

        public async Task BackupApplicationsAsync(IEnumerable<GraphApplication> applications, string defaultClaimsAccessToken = null)
        {
            try
            {
                var savePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "EnterpriseAppsBackup",
                    $"backup_{DateTime.Now:yyyyMMddHHmmss}.json"
                );

                // Create directory if it doesn't exist
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));

                var apps = applications.ToList();
                await LogAsync($"Starting backup of {apps.Count} applications to {savePath}");
                var backups = new List<ApplicationBackup>();

                foreach (var app in apps)
                {
                    await LogAsync($"Backing up application: {app.DisplayName}");

                    var backup = new ApplicationBackup
                    {
                        Application = app,
                        BackupDate = DateTime.UtcNow,
                        UserAssignments = new List<ServicePrincipalUserAssignment>(),
                        GroupAssignments = new List<ServicePrincipalGroupAssignment>(),
                        AppRoleAssignments = new List<AppRoleAssignment>()
                    };

                    try
                    {
                        // Get full application details including credentials
                        var fullApp = await GetApplicationByAppIdAsync(app.AppId);
                        if (fullApp != null)
                        {
                            backup.Application = fullApp;
                            await LogAsync($"Retrieved full application details for {fullApp.DisplayName}");
                        }

                        var servicePrincipal = await GetServicePrincipalAsync(app.AppId);
                        if (servicePrincipal != null)
                        {
                            await LogAsync($"Found service principal for {app.DisplayName}");

                            // Create a clean ServicePrincipal object without backing store
                            backup.ServicePrincipal = new ServicePrincipal
                            {
                                AppId = servicePrincipal.AppId,
                                Id = servicePrincipal.Id,
                                DisplayName = servicePrincipal.DisplayName,
                                ServicePrincipalType = servicePrincipal.ServicePrincipalType,
                                LoginUrl = servicePrincipal.LoginUrl,
                                ReplyUrls = backup.Application?.Web?.RedirectUris?.ToList() ?? new List<string>(),
                                PreferredSingleSignOnMode = servicePrincipal.PreferredSingleSignOnMode,
                                Homepage = servicePrincipal.Homepage,
                                Notes = servicePrincipal.Notes,
                                LogoutUrl = servicePrincipal.LogoutUrl,
                                SamlSingleSignOnSettings = servicePrincipal.SamlSingleSignOnSettings
                            };

                            // Backup user assignments
                            await LogAsync("Backing up user assignments...");
                            try
                            {
                                var userAssignments = await _graphClient.ServicePrincipals[servicePrincipal.Id].AppRoleAssignedTo
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Select = new[] { "id", "principalId", "principalDisplayName", "appRoleId", "principalType" };
                                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                    });

                                if (userAssignments?.Value != null)
                                {
                                    foreach (var assignment in userAssignments.Value)
                                    {
                                        // Filter for user assignments
                                        if (assignment.PrincipalType?.ToString().Equals("User", StringComparison.OrdinalIgnoreCase) == true)
                                        {
                                            await LogAsync($"Found user assignment: {assignment.PrincipalDisplayName}");
                                            backup.UserAssignments.Add(new ServicePrincipalUserAssignment
                                            {
                                                UserId = assignment.PrincipalId?.ToString(),
                                                PrincipalDisplayName = assignment.PrincipalDisplayName,
                                                AppRoleId = assignment.AppRoleId?.ToString()
                                            });
                                        }
                                    }
                                    await LogAsync($"Total user assignments found: {backup.UserAssignments.Count}");
                                }
                            }
                            catch (Exception ex)
                            {
                                await LogAsync($"Error backing up user assignments: {ex.Message}", true);
                            }

                            // Backup group assignments
                            await LogAsync("Backing up group assignments...");
                            try
                            {
                                var groupAssignments = await _graphClient.ServicePrincipals[servicePrincipal.Id].AppRoleAssignedTo
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Select = new[] { "id", "principalId", "principalDisplayName", "appRoleId", "principalType" };
                                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                    });

                                if (groupAssignments?.Value != null)
                                {
                                    foreach (var assignment in groupAssignments.Value)
                                    {
                                        // Filter for group assignments
                                        if (assignment.PrincipalType?.ToString().Equals("Group", StringComparison.OrdinalIgnoreCase) == true)
                                        {
                                            await LogAsync($"Found group assignment: {assignment.PrincipalDisplayName}");
                                            backup.GroupAssignments.Add(new ServicePrincipalGroupAssignment
                                            {
                                                GroupId = assignment.PrincipalId?.ToString(),
                                                GroupDisplayName = assignment.PrincipalDisplayName,
                                                AppRoleId = assignment.AppRoleId?.ToString()
                                            });
                                        }
                                    }
                                    await LogAsync($"Total group assignments found: {backup.GroupAssignments.Count}");
                                }
                            }
                            catch (Exception ex)
                            {
                                await LogAsync($"Error backing up group assignments: {ex.Message}", true);
                            }

                            // Backup claims configuration
                            await LogAsync("Backing up claims configuration...");
                            try
                            {
                                // Get standard claims mapping policies
                                await LogAsync("Fetching standard claims mapping policies...");
                                var standardPolicies = await _graphClient.Policies.ClaimsMappingPolicies
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Select = new[] { "id", "displayName", "definition" };
                                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                    });

                                if (standardPolicies?.Value?.Any() == true)
                                {
                                    await LogAsync($"Found {standardPolicies.Value.Count} standard claims mapping policies");
                                }

                                // Get assigned policies and SAML settings
                                await LogAsync("Fetching service principal details...");
                                var spDetails = await _graphClient.ServicePrincipals[servicePrincipal.Id]
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Select = new[] {
                                            "id",
                                            "preferredSingleSignOnMode",
                                            "samlSingleSignOnSettings"
                                        };
                                    });

                                if (spDetails != null)
                                {
                                    await LogAsync($"Application SSO mode: {spDetails.PreferredSingleSignOnMode}");
                                    if (spDetails.PreferredSingleSignOnMode?.ToString().Equals("saml", StringComparison.OrdinalIgnoreCase) == true)
                                    {
                                        await LogAsync("Application is SAML-based, fetching SAML configuration...");
                                        var samlSettings = await _graphClient.ServicePrincipals[servicePrincipal.Id]
                                            .GetAsync(requestConfiguration =>
                                            {
                                                requestConfiguration.QueryParameters.Select = new[] { "samlSingleSignOnSettings" };
                                            });

                                        if (samlSettings?.SamlSingleSignOnSettings != null)
                                        {
                                            await LogAsync($"Found SAML SSO settings with relay state: {samlSettings.SamlSingleSignOnSettings.RelayState}");
                                            backup.ServicePrincipal.SamlSingleSignOnSettings = samlSettings.SamlSingleSignOnSettings;
                                        }
                                    }
                                }

                                // Get claims configuration using device code token if available
                                if (!string.IsNullOrEmpty(defaultClaimsAccessToken))
                                {
                                    await LogAsync("Fetching application claims configuration...");
                                    var tokenCredential = new TokenCredential(defaultClaimsAccessToken);
                                    var requestAdapter = new HttpClientRequestAdapter(tokenCredential);
                                    var claimsClient = new GraphServiceClient(requestAdapter);

                                    var webConfig = await claimsClient.Applications[backup.Application.Id].GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Select = new[] { "web" };
                                    });

                                    if (webConfig?.Web != null)
                                    {
                                        await LogAsync("Found web configuration");
                                        if (webConfig.Web.ImplicitGrantSettings != null)
                                        {
                                            await LogAsync("Found implicit grant settings");
                                        }
                                        backup.Application.Web = webConfig.Web;
                                    }

                                    var spSettings = await claimsClient.ServicePrincipals[servicePrincipal.Id].GetAsync();
                                    if (spSettings != null)
                                    {
                                        await LogAsync("Found service principal settings");
                                        backup.ServicePrincipal.ClaimsMappingPolicies = spSettings.ClaimsMappingPolicies;
                                    }

                                    // Get user claims configuration
                                    var userClaims = await claimsClient.ServicePrincipals[servicePrincipal.Id].ClaimsMappingPolicies
                                        .GetAsync(requestConfiguration =>
                                        {
                                            requestConfiguration.QueryParameters.Select = new[] { "definition" };
                                        });

                                    if (userClaims?.Value != null)
                                    {
                                        await LogAsync("Found user claims configuration");
                                        foreach (var claim in userClaims.Value)
                                        {
                                            if (claim.Definition != null)
                                            {
                                                foreach (var def in claim.Definition)
                                                {
                                                    if (def.Contains("groups", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        await LogAsync("Found SAML2 claim: groups (Properties: cloud_displayname)");
                                                        await LogAsync("Found group membership claims: ApplicationGroup");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                await LogAsync($"Error backing up claims configuration: {ex.Message}", true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await LogAsync($"Error backing up application {app.DisplayName}: {ex.Message}", true);
                        continue;
                    }

                    backups.Add(backup);
                }

                var json = JsonSerializer.Serialize(backups, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                await File.WriteAllTextAsync(savePath, json);
                await LogAsync($"Backup completed successfully. Saved to: {savePath}");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error during backup: {ex.Message}";
                await LogAsync(errorMessage, true);
                if (ex.InnerException != null)
                {
                    await LogAsync($"Inner exception: {ex.InnerException.Message}", true);
                }
                throw new Exception(errorMessage, ex);
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
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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
                var errorMessage = $"Error loading backup: {ex.Message}";
                await LogAsync(errorMessage, true);
                if (ex.InnerException != null)
                {
                    await LogAsync($"Inner exception: {ex.InnerException.Message}", true);
                }
                throw new Exception(errorMessage, ex);
            }
        }

        public async Task<string> GetTenantId()
        {
            try
            {
                // Get organization details which includes tenant ID
                var org = await _graphClient.Organization
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Select = new[] { "id" };
                    });

                var tenantId = org?.Value?.FirstOrDefault()?.Id;
                if (string.IsNullOrEmpty(tenantId))
                {
                    throw new Exception("Could not retrieve tenant ID from organization details.");
                }
                return tenantId;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error getting tenant ID: {ex.Message}";
                throw new Exception(errorMessage, ex);
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
                    AppRoleAssignmentRequired = sp.AppRoleAssignmentRequired,
                    SamlSingleSignOnSettings = sp.SamlSingleSignOnSettings
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
                            await LogAsync($"Error restoring certificate {cert.DisplayName}: {ex.Message}", true);
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
                            await LogAsync($"Error restoring secret {secret.DisplayName}: {ex.Message}", true);
                        }
                    }
                }

                // Restore app role assignments
                if (backup.AppRoleAssignments?.Any() == true)
                {
                    await LogAsync($"Restoring {backup.AppRoleAssignments.Count} role assignments");
                    foreach (var assignment in backup.AppRoleAssignments)
                    {
                        try
                        {
                            // Handle the nullable Guid conversions
                            Guid? appRoleId = null;
                            Guid? principalId = null;
                            Guid? resourceId = null;

                            if (assignment.AppRoleId != null)
                            {
                                appRoleId = Guid.Parse(assignment.AppRoleId.ToString());
                            }
                            if (assignment.PrincipalId != null)
                            {
                                principalId = Guid.Parse(assignment.PrincipalId.ToString());
                            }
                            if (newSp.Id != null)
                            {
                                resourceId = Guid.Parse(newSp.Id);
                            }

                            await _graphClient.ServicePrincipals[newSp.Id].AppRoleAssignments.PostAsync(new AppRoleAssignment
                            {
                                PrincipalId = principalId,
                                ResourceId = resourceId,
                                AppRoleId = appRoleId
                            });

                            await LogAsync($"Restored role assignment for principal: {assignment.PrincipalId}");
                        }
                        catch (Exception ex)
                        {
                            await LogAsync($"Error restoring role assignment: {ex.Message}", true);
                        }
                    }
                }

                // Restore claims mapping
                if (backup.ClaimsMapping != null)
                {
                    await LogAsync("Restoring claims mapping policy");
                    var odataIdString = $"https://graph.microsoft.com/v1.0/policies/claimsMappingPolicies/{backup.ClaimsMapping.Id}";
                    await _graphClient.ServicePrincipals[newSp.Id].ClaimsMappingPolicies.Ref.PostAsync(new ReferenceCreate
                    {
                        OdataId = odataIdString
                    });
                }

                // Restore provisioning configuration
                if (backup.ProvisioningConfig?.ProvisioningSettings != null)
                {
                    await LogAsync("Restoring provisioning configuration");
                    await _graphClient.ServicePrincipals[newSp.Id].Synchronization.Templates.PostAsync(new SynchronizationTemplate
                    {
                        Schema = backup.ProvisioningConfig.ProvisioningSettings
                    });
                }

                await LogAsync($"Successfully restored application: {sp.DisplayName}");
            }
            catch (Exception ex)
            {
                await LogAsync($"Error restoring application {backup?.ServicePrincipal?.DisplayName}: {ex.Message}", true);
                if (ex.InnerException != null)
                {
                    await LogAsync($"Inner exception: {ex.InnerException.Message}", true);
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
            if (access == null || !access.Any()) return string.Empty;
            return JsonSerializer.Serialize(access, new JsonSerializerOptions { WriteIndented = true });
        }

        public string FormatApiSettings(ApiApplication api)
        {
            if (api == null) return string.Empty;
            return JsonSerializer.Serialize(api, new JsonSerializerOptions { WriteIndented = true });
        }

        public string FormatAppRoles(IList<AppRole> roles)
        {
            if (roles == null || !roles.Any()) return string.Empty;
            return JsonSerializer.Serialize(roles, new JsonSerializerOptions { WriteIndented = true });
        }

        public string FormatInfo(InformationalUrl info)
        {
            if (info == null) return string.Empty;
            return JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        }

        public IList<RequiredResourceAccess> ParseResourceAccess(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<RequiredResourceAccess>();
            return JsonSerializer.Deserialize<List<RequiredResourceAccess>>(value) ?? new List<RequiredResourceAccess>();
        }

        public ApiApplication ParseApiSettings(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new ApiApplication();
            return JsonSerializer.Deserialize<ApiApplication>(value) ?? new ApiApplication();
        }

        public IList<AppRole> ParseAppRoles(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<AppRole>();
            return JsonSerializer.Deserialize<List<AppRole>>(value) ?? new List<AppRole>();
        }

        public InformationalUrl ParseInfo(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new InformationalUrl();
            return JsonSerializer.Deserialize<InformationalUrl>(value) ?? new InformationalUrl();
        }

        private async Task<ServicePrincipal> GetServicePrincipalAsync(string appId)
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
                    "passwordCredentials",
                    "notificationEmailAddresses"
                };
            });

            return response?.Value?.FirstOrDefault();
        }

        private async Task<ServicePrincipal> GetServicePrincipalByAppIdAsync(string appId)
        {
            try
            {
                var servicePrincipals = await _graphClient.ServicePrincipals
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"appId eq '{appId}'";
                        requestConfiguration.QueryParameters.Select = new[] {
                            "id",
                            "appId",
                            "displayName",
                            "servicePrincipalType",
                            "loginUrl",
                            "preferredTokenSigningKeyThumbprint",
                            "tags",
                            "notificationEmailAddresses",
                            "keyCredentials",
                            "passwordCredentials",
                            "samlSingleSignOnSettings",
                            "appRoleAssignmentRequired"
                        };
                    });

                return servicePrincipals?.Value?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                await LogAsync($"Error getting service principal for app {appId}: {ex.Message}", true);
                return null;
            }
        }

        private async Task<IEnumerable<AppRoleAssignment>> GetAppRoleAssignmentsAsync(string servicePrincipalId)
        {
            try
            {
                var assignments = await _graphClient.ServicePrincipals[servicePrincipalId].AppRoleAssignedTo
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Select = new[] {
                            "id",
                            "principalId",
                            "principalDisplayName",
                            "appRoleId",
                            "principalType",
                            "createdDateTime",
                            "resourceId",
                            "resourceDisplayName"
                        };
                    });

                return assignments?.Value;
            }
            catch (Exception ex)
            {
                await LogAsync($"Error getting app role assignments for service principal {servicePrincipalId}: {ex.Message}", true);
                return null;
            }
        }

        private class TokenCredential : IAuthenticationProvider
        {
            private readonly string _token;

            public TokenCredential(string token)
            {
                _token = token;
            }

            public Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
            {
                request.Headers.Add("Authorization", $"Bearer {_token}");
                return Task.CompletedTask;
            }
        }
    }
}