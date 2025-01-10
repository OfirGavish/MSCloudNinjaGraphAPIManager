using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Collections.Concurrent;

namespace MSCloudNinjaGraphAPI.Services
{
    public class DirectoryService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly ConcurrentDictionary<string, string> _userCache = new();
        private readonly ConcurrentDictionary<string, string> _groupCache = new();
        private readonly ConcurrentDictionary<string, string> _appCache = new();

        public DirectoryService(GraphServiceClient graphClient)
        {
            _graphClient = graphClient;
        }

        public async Task<string> GetUserDisplayNameAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return string.Empty;
            
            if (_userCache.TryGetValue(userId, out string cachedName))
                return cachedName;

            try
            {
                var user = await _graphClient.Users[userId].GetAsync();
                var displayName = user?.DisplayName ?? userId;
                _userCache.TryAdd(userId, displayName);
                return displayName;
            }
            catch
            {
                return userId;
            }
        }

        public async Task<string> GetGroupDisplayNameAsync(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return string.Empty;

            if (_groupCache.TryGetValue(groupId, out string cachedName))
                return cachedName;

            try
            {
                var group = await _graphClient.Groups[groupId].GetAsync();
                var displayName = group?.DisplayName ?? groupId;
                _groupCache.TryAdd(groupId, displayName);
                return displayName;
            }
            catch
            {
                return groupId;
            }
        }

        public async Task<string> GetApplicationDisplayNameAsync(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return string.Empty;

            if (_appCache.TryGetValue(appId, out string cachedName))
                return cachedName;

            try
            {
                var app = await _graphClient.Applications
                    .GetAsync(requestConfiguration => 
                    {
                        requestConfiguration.QueryParameters.Filter = $"appId eq '{appId}'";
                        requestConfiguration.QueryParameters.Select = new[] { "displayName" };
                    });

                var displayName = app?.Value?.FirstOrDefault()?.DisplayName ?? appId;
                _appCache.TryAdd(appId, displayName);
                return displayName;
            }
            catch
            {
                return appId;
            }
        }
    }
}
