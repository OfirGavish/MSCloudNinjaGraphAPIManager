using Microsoft.Graph.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphApplication = Microsoft.Graph.Models.Application;

namespace MSCloudNinjaGraphAPI.Services
{
    public interface IEnterpriseAppsService
    {
        Task<List<GraphApplication>> GetApplicationsAsync();
        Task<List<ApplicationBackup>> LoadBackupAsync(string filePath);
        Task RestoreApplicationAsync(ApplicationBackup backup);
        Task SaveBackupAsync(List<GraphApplication> apps, string filePath);
        
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
    }
}
