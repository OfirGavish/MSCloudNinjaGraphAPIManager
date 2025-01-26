using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Graph.Models;
using GraphApplication = Microsoft.Graph.Models.Application;

namespace MSCloudNinjaGraphAPI
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
        [JsonPropertyName("ssoConfiguration")]
        public JsonDocument SsoConfiguration { get; set; }
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
}
