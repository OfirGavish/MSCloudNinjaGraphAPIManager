using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Authentication.Azure;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Microsoft.Kiota.Abstractions.Serialization;
using Azure.Identity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GraphApiClient
{
    public class GraphApiService
    {
        private readonly Microsoft.Graph.GraphServiceClient _graphClient;
        private const int PageSize = 999;

        public GraphServiceClient GraphClient => _graphClient;

        public GraphApiService(string clientId, string clientSecret, string tenantId)
        {
            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var tokenCredential = new AzureIdentityAuthenticationProvider(credential);
            
            var httpClient = new HttpClient();
            var requestAdapter = new HttpClientRequestAdapter(tokenCredential, httpClient: httpClient);
            requestAdapter.BaseUrl = "https://graph.microsoft.com/beta";
            
            _graphClient = new Microsoft.Graph.GraphServiceClient(requestAdapter);
        }

        public async Task<List<Microsoft.Graph.Models.ConditionalAccessPolicy>> GetConditionalAccessPoliciesAsync()
        {
            try
            {
                var policies = new List<Microsoft.Graph.Models.ConditionalAccessPolicy>();
                var response = await GraphClient.Identity.ConditionalAccess.Policies.GetAsync();

                if (response?.Value != null)
                {
                    policies.AddRange(response.Value);
                    var nextPageUrl = response.OdataNextLink;
                    while (!string.IsNullOrEmpty(nextPageUrl))
                    {
                        var nextPage = await GraphClient.Identity.ConditionalAccess.Policies
                            .WithUrl(nextPageUrl)
                            .GetAsync();
                        if (nextPage?.Value != null)
                        {
                            policies.AddRange(nextPage.Value);
                            nextPageUrl = nextPage.OdataNextLink;
                        }
                    }
                }

                return policies;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching Conditional Access Policies: {ex.Message}", ex);
            }
        }

        public async Task<(List<Microsoft.Graph.Models.User> Users, List<Microsoft.Graph.Models.Group> Groups)>
            GetUsersAndGroupsAsync()
        {
            try
            {
                var users = new List<Microsoft.Graph.Models.User>();
                var groups = new List<Microsoft.Graph.Models.Group>();

                // Get Users
                var usersResponse = await GraphClient.Users.GetAsync(config =>
                {
                    config.QueryParameters.Select = new[]
                    {
                        "id", "displayName", "userPrincipalName", "mail",
                        "accountEnabled", "department", "jobTitle"
                    };
                    config.QueryParameters.Top = PageSize;
                });

                if (usersResponse?.Value != null)
                {
                    users.AddRange(usersResponse.Value);
                    var nextPageUrl = usersResponse.OdataNextLink;
                    while (!string.IsNullOrEmpty(nextPageUrl))
                    {
                        var nextPage = await GraphClient.Users
                            .WithUrl(nextPageUrl)
                            .GetAsync();
                        if (nextPage?.Value != null)
                        {
                            users.AddRange(nextPage.Value);
                            nextPageUrl = nextPage.OdataNextLink;
                        }
                    }
                }

                // Get Groups
                var groupsResponse = await GraphClient.Groups.GetAsync(config =>
                {
                    config.QueryParameters.Select = new[]
                    {
                        "id", "displayName", "description", "groupTypes",
                        "securityEnabled", "mailEnabled"
                    };
                    config.QueryParameters.Top = PageSize;
                });

                if (groupsResponse?.Value != null)
                {
                    groups.AddRange(groupsResponse.Value);
                    var nextPageUrl = groupsResponse.OdataNextLink;
                    while (!string.IsNullOrEmpty(nextPageUrl))
                    {
                        var nextPage = await GraphClient.Groups
                            .WithUrl(nextPageUrl)
                            .GetAsync();
                        if (nextPage?.Value != null)
                        {
                            groups.AddRange(nextPage.Value);
                            nextPageUrl = nextPage.OdataNextLink;
                        }
                    }
                }

                return (users, groups);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching Users and Groups: {ex.Message}", ex);
            }
        }

        public async Task<(List<Microsoft.Graph.Models.Application> EnterpriseApps,
            List<Microsoft.Graph.Models.Application> AppRegistrations)> GetApplicationsAsync()
        {
            try
            {
                var enterpriseApps = new List<Microsoft.Graph.Models.Application>();
                var appRegistrations = new List<Microsoft.Graph.Models.Application>();

                // Get Enterprise Applications
                var enterpriseResponse = await GraphClient.Applications.GetAsync(config =>
                {
                    config.QueryParameters.Filter = "tags/any(t:t eq 'WindowsAzureActiveDirectoryIntegratedApp')";
                    config.QueryParameters.Top = PageSize;
                });

                if (enterpriseResponse?.Value != null)
                {
                    enterpriseApps.AddRange(enterpriseResponse.Value);
                    var nextPageUrl = enterpriseResponse.OdataNextLink;
                    while (!string.IsNullOrEmpty(nextPageUrl))
                    {
                        var nextPage = await GraphClient.Applications
                            .WithUrl(nextPageUrl)
                            .GetAsync();
                        if (nextPage?.Value != null)
                        {
                            enterpriseApps.AddRange(nextPage.Value);
                            nextPageUrl = nextPage.OdataNextLink;
                        }
                    }
                }

                // Get App Registrations
                var appRegResponse = await GraphClient.Applications.GetAsync(config =>
                {
                    config.QueryParameters.Filter = "tags/any(t:t eq 'AppRegistration')";
                    config.QueryParameters.Top = PageSize;
                });

                if (appRegResponse?.Value != null)
                {
                    appRegistrations.AddRange(appRegResponse.Value);
                    var nextPageUrl = appRegResponse.OdataNextLink;
                    while (!string.IsNullOrEmpty(nextPageUrl))
                    {
                        var nextPage = await GraphClient.Applications
                            .WithUrl(nextPageUrl)
                            .GetAsync();
                        if (nextPage?.Value != null)
                        {
                            appRegistrations.AddRange(nextPage.Value);
                            nextPageUrl = nextPage.OdataNextLink;
                        }
                    }
                }

                return (enterpriseApps, appRegistrations);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching Applications: {ex.Message}", ex);
            }
        }

        public async Task<List<Microsoft.Graph.Models.DeviceConfiguration>> GetIntuneConfigurationPoliciesAsync()
        {
            try
            {
                var policies = new List<Microsoft.Graph.Models.DeviceConfiguration>();
                var response = await GraphClient.DeviceManagement.DeviceConfigurations
                    .GetAsync(config =>
                    {
                        config.QueryParameters.Expand = new[]
                        {
                            "assignments", "deviceStatusOverview"
                        };
                        config.QueryParameters.Top = PageSize;
                    });

                if (response?.Value != null)
                {
                    policies.AddRange(response.Value);
                    var nextPageUrl = response.OdataNextLink;
                    while (!string.IsNullOrEmpty(nextPageUrl))
                    {
                        var nextPage = await GraphClient.DeviceManagement.DeviceConfigurations
                            .WithUrl(nextPageUrl)
                            .GetAsync();
                        if (nextPage?.Value != null)
                        {
                            policies.AddRange(nextPage.Value);
                            nextPageUrl = nextPage.OdataNextLink;
                        }
                    }
                }

                return policies;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching Intune Configuration Policies: {ex.Message}", ex);
            }
        }

        public async Task<List<object>> GetIntuneScriptsAsync()
        {
            try
            {
                var scripts = new List<object>();
                var requestInfo = new RequestInformation
                {
                    HttpMethod = Method.GET,
                    URI = new Uri("https://graph.microsoft.com/beta/deviceManagement/deviceManagementScripts")
                };

                requestInfo.Headers.Add("Accept", "application/json");
                requestInfo.Headers.Add("ConsistencyLevel", "eventual");

                var response = await _graphClient.RequestAdapter.SendAsync<DeviceManagementScriptResponse>(
                    requestInfo,
                    DeviceManagementScriptResponse.CreateFromDiscriminatorValue,
                    default);

                if (response?.Value != null)
                {
                    scripts.AddRange(response.Value);
                }

                return scripts;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching Intune Scripts: {ex.Message}", ex);
            }
        }

        private class DeviceManagementScriptResponse : IParsable
        {
            public IEnumerable<object> Value { get; set; }

            public static DeviceManagementScriptResponse CreateFromDiscriminatorValue(IParseNode parseNode)
            {
                if (parseNode == null)
                    return null;

                return new DeviceManagementScriptResponse();
            }

            public IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
            {
                return new Dictionary<string, Action<IParseNode>>
                {
                    { "value", n => Value = n.GetCollectionOfPrimitiveValues<object>() ?? Enumerable.Empty<object>() }
                };
            }

            public void Serialize(ISerializationWriter writer)
            {
                if (writer == null)
                    return;

                writer.WriteCollectionOfPrimitiveValues("value", Value);
            }
        }

        private static class Factory
        {
            public static Func<Dictionary<string, object>, object> CreateGetResponseHandler(Type type, string propertyName)
            {
                return (Dictionary<string, object> dict) =>
                {
                    if (dict.ContainsKey(propertyName))
                    {
                        return dict[propertyName];
                    }
                    return null;
                };
            }
        }
    }
}