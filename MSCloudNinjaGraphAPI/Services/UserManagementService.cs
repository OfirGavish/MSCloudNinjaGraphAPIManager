using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using System.Web;

namespace MSCloudNinjaGraphAPI.Services
{
    public interface IUserManagementService
    {
        Task<List<User>> GetAllUsersAsync();
        Task DisableUserAsync(string userId);
        Task RemoveFromGlobalAddressListAsync(string userId);
        Task RemoveFromAllGroupsAsync(string userId);
        Task UpdateManagerForEmployeesAsync(string userId);
    }

    public class UserManagementService : IUserManagementService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly LogService _logService;

        public UserManagementService(GraphServiceClient graphClient)
        {
            _graphClient = graphClient;
            _logService = new LogService();
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            var users = new List<User>();
            var pageCount = 0;

            try
            {
                var queryOptions = new[]
                {
                    "id",
                    "displayName",
                    "userPrincipalName",
                    "accountEnabled",
                    "department",
                    "jobTitle"
                };

                // Initial request
                var response = await _graphClient.Users.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = queryOptions;
                    requestConfiguration.QueryParameters.Top = 999;
                    requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                    requestConfiguration.QueryParameters.Orderby = new[] { "userPrincipalName" };
                });

                while (response?.Value != null)
                {
                    pageCount++;
                    var newUsers = response.Value.Where(u => !string.IsNullOrEmpty(u.UserPrincipalName)).ToList();
                    users.AddRange(newUsers);
                    await _logService.LogAsync($"Page {pageCount}: Loaded {newUsers.Count} users (Total: {users.Count})");

                    // Break if no more pages
                    if (string.IsNullOrEmpty(response.OdataNextLink))
                        break;

                    try
                    {
                        // Extract skipToken from OdataNextLink
                        string skipToken = response.OdataNextLink[(response.OdataNextLink.IndexOf("$skiptoken=") + "$skiptoken=".Length)..];

                        // Create request information
                        var requestInformation = _graphClient.Users.ToGetRequestInformation(requestConfiguration =>
                        {
                            requestConfiguration.QueryParameters.Select = queryOptions;
                            requestConfiguration.QueryParameters.Top = 999;
                            requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                            requestConfiguration.QueryParameters.Orderby = new[] { "userPrincipalName" };
                        });

                        // Modify URL template to include skiptoken
                        requestInformation.UrlTemplate = requestInformation.UrlTemplate[..^1] + ",%24skiptoken" + requestInformation.UrlTemplate[^1];
                        requestInformation.QueryParameters.Add("%24skiptoken", skipToken);

                        // Send request
                        response = await _graphClient.RequestAdapter.SendAsync(requestInformation, 
                            UserCollectionResponse.CreateFromDiscriminatorValue);
                    }
                    catch (Exception ex)
                    {
                        await _logService.LogAsync($"Error getting next page: {ex.Message}", true);
                        break;
                    }
                }

                await _logService.LogAsync($"Finished loading {users.Count} users from {pageCount} pages.");
                return users.OrderBy(u => u.DisplayName).ToList();
            }
            catch (Exception ex)
            {
                await _logService.LogAsync($"Error getting users: {ex.Message}", true);
                throw;
            }
        }

        public async Task DisableUserAsync(string userId)
        {
            var user = new User { AccountEnabled = false };
            await _graphClient.Users[userId].PatchAsync(user);
        }

        public async Task RemoveFromGlobalAddressListAsync(string userId)
        {
            var user = new User 
            { 
                ShowInAddressList = false
            };
            await _graphClient.Users[userId].PatchAsync(user);
        }

        public async Task RemoveFromAllGroupsAsync(string userId)
        {
            try
            {
                // Get all groups (including Microsoft 365 groups, security groups, and distribution lists)
                var memberOfGroups = await _graphClient.Users[userId].MemberOf.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Select = new[] { "id", "displayName", "groupTypes" };
                });

                if (memberOfGroups?.Value != null)
                {
                    foreach (var groupObj in memberOfGroups.Value)
                    {
                        if (groupObj.OdataType == "#microsoft.graph.group")
                        {
                            try
                            {
                                // Remove from all group types
                                await _graphClient.Groups[groupObj.Id].Members[userId].Ref.DeleteAsync();
                            }
                            catch (Exception ex)
                            {
                                // If removal from one group fails, continue with others
                                System.Diagnostics.Debug.WriteLine($"Failed to remove user from group {groupObj.Id}: {ex.Message}");
                            }
                        }
                    }
                }

                // Also check transitive membership (nested groups)
                var transitiveGroups = await _graphClient.Users[userId].TransitiveMemberOf.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Select = new[] { "id", "displayName", "groupTypes" };
                });

                if (transitiveGroups?.Value != null)
                {
                    foreach (var groupObj in transitiveGroups.Value)
                    {
                        if (groupObj.OdataType == "#microsoft.graph.group" && 
                            !memberOfGroups.Value.Any(g => g.Id == groupObj.Id)) // Skip if already processed
                        {
                            try
                            {
                                await _graphClient.Groups[groupObj.Id].Members[userId].Ref.DeleteAsync();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to remove user from transitive group {groupObj.Id}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to remove user from groups: {ex.Message}", ex);
            }
        }

        public async Task UpdateManagerForEmployeesAsync(string userId)
        {
            // Get the manager of the user being offboarded
            var manager = await _graphClient.Users[userId].Manager.GetAsync();
            if (manager == null) return;

            // Get direct reports of the user being offboarded
            var directReports = await _graphClient.Users[userId].DirectReports.GetAsync();
            if (directReports?.Value == null) return;

            // Update manager for each direct report
            foreach (var directReport in directReports.Value)
            {
                var reportUserId = directReport.Id;
                await _graphClient.Users[reportUserId].Manager.Ref.PutAsync(new ReferenceUpdate
                {
                    OdataId = $"https://graph.microsoft.com/v1.0/users/{manager.Id}"
                });
            }
        }
    }
}
