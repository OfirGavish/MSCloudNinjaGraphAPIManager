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
        [JsonPropertyName("appRoleAssignments")]
        public List<AppRoleAssignment> AppRoleAssignments { get; set; }
        [JsonPropertyName("claimsMapping")]
        public ClaimsMappingPolicy ClaimsMapping { get; set; }
        [JsonPropertyName("provisioningConfig")]
        public ProvisioningConfig ProvisioningConfig { get; set; }
        [JsonPropertyName("userAssignments")]
        public List<ServicePrincipalUserAssignment> UserAssignments { get; set; }
        [JsonPropertyName("groupAssignments")]
        public List<ServicePrincipalGroupAssignment> GroupAssignments { get; set; }
        [JsonPropertyName("samlConfiguration")]
        public SamlConfiguration SamlConfiguration { get; set; }
    }

    public class ProvisioningConfig
    {
        [JsonPropertyName("provisioningSettings")]
        public SynchronizationSchema ProvisioningSettings { get; set; }
        [JsonPropertyName("provisioningStatus")]
        public SynchronizationStatus? ProvisioningStatus { get; set; }
    }

    public class ServicePrincipalUserAssignment
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }
        [JsonPropertyName("principalDisplayName")]
        public string PrincipalDisplayName { get; set; }
        [JsonPropertyName("appRoleId")]
        public string AppRoleId { get; set; }
    }

    public class ServicePrincipalGroupAssignment
    {
        [JsonPropertyName("groupId")]
        public string GroupId { get; set; }
        [JsonPropertyName("groupDisplayName")]
        public string GroupDisplayName { get; set; }
        [JsonPropertyName("appRoleId")]
        public string AppRoleId { get; set; }
    }

    public class SamlConfiguration
    {
        [JsonPropertyName("samlSingleSignOnSettings")]
        public SamlSingleSignOnSettings SamlSingleSignOnSettings { get; set; }
        [JsonPropertyName("claimsMappings")]
        public List<ClaimsMappingPolicy> ClaimsMappings { get; set; }
        [JsonPropertyName("optionalClaims")]
        public OptionalClaims OptionalClaims { get; set; }
        [JsonPropertyName("customAttributes")]
        public object CustomAttributes { get; set; }
    }

    public class EnterpriseAppsService : IEnterpriseAppsService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly LogService _logService;

        public EnterpriseAppsService(GraphServiceClient graphClient)
        {
            _graphClient = graphClient;
            _logService = new LogService();
        }

        private async Task LogAsync(string message, bool isError = false)
        {
            await _logService.LogAsync(message, isError);
        }

        public async Task<List<GraphApplication>> GetApplicationsAsync()
        {
            try
            {
                var apps = new List<GraphApplication>();
                var response = await _graphClient.Applications.GetAsync(config =>
                {
                    config.QueryParameters.Top = 999;
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
                                                    .Select(c => new { 
                                                        c.Name, 
                                                        c.Essential, 
                                                        c.AdditionalProperties, 
                                                        c.Source
                                                    }).ToList(),
                                                IdToken = appWithClaims.OptionalClaims.IdToken?
                                                    .Select(c => new { 
                                                        c.Name, 
                                                        c.Essential, 
                                                        c.AdditionalProperties, 
                                                        c.Source
                                                    }).ToList(),
                                                Saml2Token = appWithClaims.OptionalClaims.Saml2Token?
                                                    .Select(c => new { 
                                                        c.Name, 
                                                        c.Essential, 
                                                        c.AdditionalProperties, 
                                                        c.Source
                                                    }).ToList()
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
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                };
                
                var json = JsonSerializer.Serialize(backups, options);
                await File.WriteAllTextAsync(filePath, json);
                await LogAsync($"Backup completed successfully. Saved to: {filePath}");
            }
            catch (Exception ex)
            {
                await LogAsync($"Error saving backup: {ex.Message}", true);
                if (ex.InnerException != null)
                {
                    await LogAsync($"Inner exception: {ex.InnerException.Message}", true);
                }
                throw;
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