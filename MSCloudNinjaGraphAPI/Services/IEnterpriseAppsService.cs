using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using MSCloudNinjaGraphAPI.Models;
using GraphApplication = Microsoft.Graph.Models.Application;

namespace MSCloudNinjaGraphAPI.Services
{
    public interface IEnterpriseAppsService
    {
        Task<List<GraphApplication>> GetApplicationsAsync();
        Task<List<ApplicationBackup>> LoadBackupAsync(string filePath);
        Task RestoreApplicationAsync(ApplicationBackup backup);
        Task SaveBackupAsync(List<GraphApplication> apps, string filePath);
        Task BackupApplicationsAsync(IEnumerable<GraphApplication> applications, string defaultClaimsAccessToken = null);
        
        // Formatting methods
        string FormatResourceAccess(IList<RequiredResourceAccess> access);
        string FormatApiSettings(ApiApplication api);
        string FormatAppRoles(IList<AppRole> roles);
        string FormatInfo(InformationalUrl info);
        
        // Parsing methods
        IList<RequiredResourceAccess> ParseResourceAccess(string value);
        ApiApplication ParseApiSettings(string value);
        IList<AppRole> ParseAppRoles(string value);
        InformationalUrl ParseInfo(string value);
        
        Task<string> GetTenantId();
    }
}
