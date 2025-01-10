using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Text.Json;
using System.IO;

namespace MSCloudNinjaGraphAPI.Services
{
    public class ConditionalAccessService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly DirectoryService _directoryService;

        public ConditionalAccessService(GraphServiceClient graphClient)
        {
            _graphClient = graphClient;
            _directoryService = new DirectoryService(graphClient);
        }

        public async Task<List<ConditionalAccessPolicy>> GetAllPoliciesAsync()
        {
            try
            {
                var policies = await _graphClient.Identity.ConditionalAccess.Policies
                    .GetAsync();

                return policies?.Value?.ToList() ?? new List<ConditionalAccessPolicy>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching Conditional Access policies: {ex.Message}", ex);
            }
        }

        public async Task BackupPoliciesAsync(List<ConditionalAccessPolicy> policies, string backupPath)
        {
            if (!Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFile = Path.Combine(backupPath, $"ConditionalAccessPolicies_{timestamp}.json");

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(policies, options);
            await File.WriteAllTextAsync(backupFile, json);
        }

        public async Task<List<ConditionalAccessPolicy>> LoadBackupAsync(string backupFile)
        {
            try
            {
                var json = await File.ReadAllTextAsync(backupFile);
                return JsonSerializer.Deserialize<List<ConditionalAccessPolicy>>(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading backup file: {ex.Message}", ex);
            }
        }

        public async Task RestorePolicyAsync(ConditionalAccessPolicy policy)
        {
            try
            {
                // Create a clean policy object with required properties
                var cleanPolicy = new ConditionalAccessPolicy
                {
                    DisplayName = policy.DisplayName,
                    State = policy.State,
                    Conditions = new ConditionalAccessConditionSet
                    {
                        Applications = policy.Conditions?.Applications,
                        Users = policy.Conditions?.Users,
                        Platforms = policy.Conditions?.Platforms,
                        Locations = policy.Conditions?.Locations,
                        ClientApplications = policy.Conditions?.ClientApplications,
                        Devices = policy.Conditions?.Devices,
                        UserRiskLevels = policy.Conditions?.UserRiskLevels,
                        SignInRiskLevels = policy.Conditions?.SignInRiskLevels,
                        ClientAppTypes = policy.Conditions?.ClientAppTypes
                    },
                    GrantControls = new ConditionalAccessGrantControls
                    {
                        BuiltInControls = policy.GrantControls?.BuiltInControls,
                        CustomAuthenticationFactors = policy.GrantControls?.CustomAuthenticationFactors,
                        Operator = policy.GrantControls?.Operator,
                        TermsOfUse = policy.GrantControls?.TermsOfUse
                    }
                };

                // Add session controls if they exist
                if (policy.SessionControls != null)
                {
                    cleanPolicy.SessionControls = new ConditionalAccessSessionControls
                    {
                        ApplicationEnforcedRestrictions = policy.SessionControls?.ApplicationEnforcedRestrictions,
                        CloudAppSecurity = policy.SessionControls?.CloudAppSecurity,
                        DisableResilienceDefaults = policy.SessionControls?.DisableResilienceDefaults,
                        PersistentBrowser = policy.SessionControls?.PersistentBrowser,
                        SignInFrequency = policy.SessionControls?.SignInFrequency
                    };
                }

                // Check if policy exists
                var existingPolicy = await _graphClient.Identity.ConditionalAccess.Policies
                    .GetAsync(requestConfiguration => 
                    {
                        requestConfiguration.QueryParameters.Filter = $"displayName eq '{policy.DisplayName}'";
                    });

                if (existingPolicy?.Value?.Any() == true)
                {
                    // Update existing policy
                    var policyId = existingPolicy.Value.First().Id;
                    await _graphClient.Identity.ConditionalAccess.Policies[policyId]
                        .PatchAsync(cleanPolicy);
                }
                else
                {
                    // Create new policy
                    await _graphClient.Identity.ConditionalAccess.Policies
                        .PostAsync(cleanPolicy);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error restoring policy '{policy.DisplayName}': {ex.Message}");
            }
        }

        public async Task<ConditionalAccessPolicy> GetPolicyDetailsAsync(string policyId)
        {
            try
            {
                return await _graphClient.Identity.ConditionalAccess.Policies[policyId]
                    .GetAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching policy details for ID {policyId}: {ex.Message}", ex);
            }
        }

        public async Task<List<string>> GetBackupFilesAsync(string backupDirectory)
        {
            if (!Directory.Exists(backupDirectory))
                return new List<string>();

            return Directory.GetFiles(backupDirectory, "ConditionalAccessPolicies_*.json")
                .OrderByDescending(f => f)
                .ToList();
        }

        public async Task<string> FormatPolicyDetailsAsync(ConditionalAccessPolicy policy)
        {
            var details = new System.Text.StringBuilder();
            details.AppendLine($"Policy Name: {policy.DisplayName}");
            details.AppendLine($"State: {policy.State}");
            details.AppendLine();

            details.AppendLine("CONDITIONS");
            details.AppendLine("==========");

            if (policy.Conditions != null)
            {
                // Users and Groups
                details.AppendLine("\n📱 User and Group Conditions");
                details.AppendLine("-------------------------");
                if (policy.Conditions.Users != null)
                {
                    details.AppendLine("  Users:");
                    if (policy.Conditions.Users.IncludeUsers?.Any() == true)
                    {
                        details.AppendLine("    ✓ Included Users:");
                        foreach (var userId in policy.Conditions.Users.IncludeUsers)
                        {
                            var name = await _directoryService.GetUserDisplayNameAsync(userId);
                            details.AppendLine($"      - {name} ({userId})");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No Included Users");
                    }

                    if (policy.Conditions.Users.ExcludeUsers?.Any() == true)
                    {
                        details.AppendLine("    ✓ Excluded Users:");
                        foreach (var userId in policy.Conditions.Users.ExcludeUsers)
                        {
                            var name = await _directoryService.GetUserDisplayNameAsync(userId);
                            details.AppendLine($"      - {name} ({userId})");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No Excluded Users");
                    }

                    if (policy.Conditions.Users.IncludeGroups?.Any() == true)
                    {
                        details.AppendLine("    ✓ Included Groups:");
                        foreach (var groupId in policy.Conditions.Users.IncludeGroups)
                        {
                            var name = await _directoryService.GetGroupDisplayNameAsync(groupId);
                            details.AppendLine($"      - {name} ({groupId})");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No Included Groups");
                    }

                    if (policy.Conditions.Users.ExcludeGroups?.Any() == true)
                    {
                        details.AppendLine("    ✓ Excluded Groups:");
                        foreach (var groupId in policy.Conditions.Users.ExcludeGroups)
                        {
                            var name = await _directoryService.GetGroupDisplayNameAsync(groupId);
                            details.AppendLine($"      - {name} ({groupId})");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No Excluded Groups");
                    }

                    if (policy.Conditions.Users.IncludeRoles?.Any() == true)
                    {
                        details.AppendLine("    ✓ Included Roles:");
                        foreach (var role in policy.Conditions.Users.IncludeRoles)
                        {
                            details.AppendLine($"      - {role}");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No Included Roles");
                    }

                    if (policy.Conditions.Users.ExcludeRoles?.Any() == true)
                    {
                        details.AppendLine("    ✓ Excluded Roles:");
                        foreach (var role in policy.Conditions.Users.ExcludeRoles)
                        {
                            details.AppendLine($"      - {role}");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No Excluded Roles");
                    }
                }

                // Applications
                details.AppendLine("\n🔌 Application Conditions");
                details.AppendLine("----------------------");
                if (policy.Conditions.Applications != null)
                {
                    if (policy.Conditions.Applications.IncludeApplications?.Any() == true)
                    {
                        details.AppendLine("    ✓ Included Applications:");
                        foreach (var appId in policy.Conditions.Applications.IncludeApplications)
                        {
                            var name = await _directoryService.GetApplicationDisplayNameAsync(appId);
                            details.AppendLine($"      - {name} ({appId})");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No Included Applications");
                    }

                    if (policy.Conditions.Applications.ExcludeApplications?.Any() == true)
                    {
                        details.AppendLine("    ✓ Excluded Applications:");
                        foreach (var appId in policy.Conditions.Applications.ExcludeApplications)
                        {
                            var name = await _directoryService.GetApplicationDisplayNameAsync(appId);
                            details.AppendLine($"      - {name} ({appId})");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No Excluded Applications");
                    }

                    if (policy.Conditions.Applications.IncludeUserActions?.Any() == true)
                    {
                        details.AppendLine("    ✓ User Actions:");
                        foreach (var action in policy.Conditions.Applications.IncludeUserActions)
                        {
                            details.AppendLine($"      - {action}");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No User Actions");
                    }
                }

                // Platforms
                details.AppendLine("\n💻 Platform Conditions");
                details.AppendLine("-------------------");
                if (policy.Conditions.Platforms != null)
                {
                    if (policy.Conditions.Platforms.IncludePlatforms?.Any() == true)
                    {
                        details.AppendLine("    ✓ Included Platforms:");
                        foreach (var platform in policy.Conditions.Platforms.IncludePlatforms)
                        {
                            details.AppendLine($"      - {platform}");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No Included Platforms");
                    }

                    if (policy.Conditions.Platforms.ExcludePlatforms?.Any() == true)
                    {
                        details.AppendLine("    ✓ Excluded Platforms:");
                        foreach (var platform in policy.Conditions.Platforms.ExcludePlatforms)
                        {
                            details.AppendLine($"      - {platform}");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No Excluded Platforms");
                    }
                }

                // Locations
                details.AppendLine("\n🌍 Location Conditions");
                details.AppendLine("-------------------");
                if (policy.Conditions.Locations != null)
                {
                    if (policy.Conditions.Locations.IncludeLocations?.Any() == true)
                    {
                        details.AppendLine("    ✓ Included Locations:");
                        foreach (var location in policy.Conditions.Locations.IncludeLocations)
                        {
                            details.AppendLine($"      - {location}");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No Included Locations");
                    }

                    if (policy.Conditions.Locations.ExcludeLocations?.Any() == true)
                    {
                        details.AppendLine("    ✓ Excluded Locations:");
                        foreach (var location in policy.Conditions.Locations.ExcludeLocations)
                        {
                            details.AppendLine($"      - {location}");
                        }
                    }
                    else
                    {
                        details.AppendLine("    ✗ No Excluded Locations");
                    }
                }

                // Client Applications
                details.AppendLine("\n📱 Client App Conditions");
                details.AppendLine("---------------------");
                if (policy.Conditions.ClientAppTypes?.Any() == true)
                {
                    details.AppendLine("    ✓ Included Client Apps:");
                    foreach (var appType in policy.Conditions.ClientAppTypes)
                    {
                        details.AppendLine($"      - {appType}");
                    }
                }
                else
                {
                    details.AppendLine("    ✗ No Client App Types Specified");
                }

                // Devices
                details.AppendLine("\n📱 Device Conditions");
                details.AppendLine("------------------");
                if (policy.Conditions.Devices != null)
                {
                    details.AppendLine($"    Device Filter Mode: {(policy.Conditions.Devices.DeviceFilter?.Mode.HasValue == true ? policy.Conditions.Devices.DeviceFilter.Mode.ToString() : "Not Set")}");
                    details.AppendLine($"    Device Filter Rule: {(string.IsNullOrEmpty(policy.Conditions.Devices.DeviceFilter?.Rule) ? "None" : policy.Conditions.Devices.DeviceFilter.Rule)}");
                }
                else
                {
                    details.AppendLine("    ✗ No Device Conditions");
                }

                // Risk Levels
                details.AppendLine("\n⚠️ Risk Conditions");
                details.AppendLine("----------------");
                if (policy.Conditions.UserRiskLevels?.Any() == true)
                {
                    details.AppendLine("    ✓ User Risk Levels:");
                    foreach (var risk in policy.Conditions.UserRiskLevels)
                    {
                        details.AppendLine($"      - {risk}");
                    }
                }
                else
                {
                    details.AppendLine("    ✗ No User Risk Levels");
                }

                if (policy.Conditions.SignInRiskLevels?.Any() == true)
                {
                    details.AppendLine("    ✓ Sign-in Risk Levels:");
                    foreach (var risk in policy.Conditions.SignInRiskLevels)
                    {
                        details.AppendLine($"      - {risk}");
                    }
                }
                else
                {
                    details.AppendLine("    ✗ No Sign-in Risk Levels");
                }
            }

            // Grant Controls
            details.AppendLine("\nGRANT CONTROLS");
            details.AppendLine("==============");
            if (policy.GrantControls != null)
            {
                details.AppendLine($"\nOperator: {policy.GrantControls.Operator}");
                
                if (policy.GrantControls.BuiltInControls?.Any() == true)
                {
                    details.AppendLine("✓ Built-in Controls:");
                    foreach (var control in policy.GrantControls.BuiltInControls)
                    {
                        details.AppendLine($"  - {control}");
                    }
                }
                else
                {
                    details.AppendLine("✗ No Built-in Controls");
                }

                if (policy.GrantControls.CustomAuthenticationFactors?.Any() == true)
                {
                    details.AppendLine("✓ Custom Authentication Factors:");
                    foreach (var factor in policy.GrantControls.CustomAuthenticationFactors)
                    {
                        details.AppendLine($"  - {factor}");
                    }
                }
                else
                {
                    details.AppendLine("✗ No Custom Authentication Factors");
                }

                if (policy.GrantControls.TermsOfUse?.Any() == true)
                {
                    details.AppendLine("✓ Terms of Use:");
                    foreach (var term in policy.GrantControls.TermsOfUse)
                    {
                        details.AppendLine($"  - {term}");
                    }
                }
                else
                {
                    details.AppendLine("✗ No Terms of Use");
                }
            }
            else
            {
                details.AppendLine("✗ No Grant Controls Configured");
            }

            // Session Controls
            details.AppendLine("\nSESSION CONTROLS");
            details.AppendLine("================");
            if (policy.SessionControls != null)
            {
                if (policy.SessionControls.ApplicationEnforcedRestrictions != null)
                {
                    details.AppendLine("✓ App Enforced Restrictions Enabled");
                }
                else
                {
                    details.AppendLine("✗ No App Enforced Restrictions");
                }

                if (policy.SessionControls.CloudAppSecurity != null)
                {
                    details.AppendLine($"✓ Cloud App Security: {policy.SessionControls.CloudAppSecurity.CloudAppSecurityType}");
                }
                else
                {
                    details.AppendLine("✗ No Cloud App Security Controls");
                }

                if (policy.SessionControls.SignInFrequency != null)
                {
                    details.AppendLine($"✓ Sign-in Frequency: {policy.SessionControls.SignInFrequency.Value} {policy.SessionControls.SignInFrequency.Type}");
                }
                else
                {
                    details.AppendLine("✗ No Sign-in Frequency Controls");
                }

                if (policy.SessionControls.PersistentBrowser != null)
                {
                    details.AppendLine($"✓ Persistent Browser: {policy.SessionControls.PersistentBrowser.Mode}");
                }
                else
                {
                    details.AppendLine("✗ No Persistent Browser Controls");
                }
            }
            else
            {
                details.AppendLine("✗ No Session Controls Configured");
            }

            return details.ToString();
        }
    }
}
