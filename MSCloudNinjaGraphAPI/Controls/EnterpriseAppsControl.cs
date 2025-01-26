using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using MSCloudNinjaGraphAPI.Services;
using MSCloudNinjaGraphAPI.Models;
using MSCloudNinjaGraphAPI.Controls.Components;
using GraphApplication = Microsoft.Graph.Models.Application;
using Azure.Identity;
using Azure.Core;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using System.Diagnostics;

namespace MSCloudNinjaGraphAPI.Controls
{
    public partial class EnterpriseAppsControl : UserControl
    {
        private readonly GraphServiceClient _graphClient;
        private readonly IEnterpriseAppsService _enterpriseAppsService;
        private readonly EnterpriseAppsButtonPanel _buttonPanel;
        private readonly EnterpriseAppsSearchPanel _searchPanel;
        private readonly EnterpriseAppsStatusPanel _statusPanel;
        private readonly EnterpriseAppsDialogManager _dialogManager;
        private EnterpriseAppsGridManager _gridManager;
        private EnterpriseAppsBackupManager _backupManager;
        private List<GraphApplication> _apps;
        private DataGridView appsGrid;
        private CheckBox chkBackupDefaultClaims;

        public EnterpriseAppsControl(GraphServiceClient graphClient, IEnterpriseAppsService enterpriseAppsService)
            : base()
        {
            InitializeComponent();
            
            _graphClient = graphClient;
            _enterpriseAppsService = enterpriseAppsService;
            _buttonPanel = new EnterpriseAppsButtonPanel();
            _searchPanel = new EnterpriseAppsSearchPanel();
            _statusPanel = new EnterpriseAppsStatusPanel();
            _dialogManager = new EnterpriseAppsDialogManager();
            _apps = new List<GraphApplication>();

            InitializeGrid();
            InitializeComponents();
            RegisterEventHandlers();
            RefreshGridAsync().ConfigureAwait(false);
        }

        private void InitializeGrid()
        {
            appsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = true,
                AllowUserToResizeRows = false,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            Controls.Add(appsGrid);
        }

        private void InitializeComponents()
        {
            _gridManager = new EnterpriseAppsGridManager(appsGrid, _enterpriseAppsService);
            _backupManager = new EnterpriseAppsBackupManager(_enterpriseAppsService, _dialogManager);

            // Add controls in correct order for proper docking
            Controls.Add(_statusPanel);  // Bottom
            Controls.Add(appsGrid);      // Fill
            Controls.Add(_searchPanel);   // Top
            Controls.Add(_buttonPanel);   // Top
        }

        private void RegisterEventHandlers()
        {
            _buttonPanel.SelectAllClicked += _buttonPanel_SelectAllClicked;
            _buttonPanel.LoadBackupClicked += _buttonPanel_LoadBackupClicked;
            _buttonPanel.RestoreAppsClicked += _buttonPanel_RestoreAppsClicked;
            _buttonPanel.BackupAppsClicked += _buttonPanel_BackupAppsClicked;

            _backupManager.StatusUpdated += BackupManager_StatusUpdated;
            _backupManager.ErrorOccurred += BackupManager_ErrorOccurred;
            _backupManager.BackupLoaded += BackupManager_BackupLoaded;

            _gridManager.RowCountChanged += GridManager_RowCountChanged;

            _searchPanel.RefreshClicked += SearchPanel_RefreshClicked;
            _searchPanel.SearchTextChanged += SearchPanel_SearchTextChanged;
        }

        private void _buttonPanel_SelectAllClicked(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in appsGrid.Rows)
            {
                row.Cells["Select"].Value = true;
            }
        }

        private async void _buttonPanel_LoadBackupClicked(object sender, EventArgs e)
        {
            await _backupManager.LoadBackupAsync();
        }

        private async void _buttonPanel_RestoreAppsClicked(object sender, EventArgs e)
        {
            var selectedApps = _gridManager.GetSelectedApplications();
            if (!selectedApps.Any())
            {
                MessageBox.Show("Please select at least one application to restore.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to restore {selectedApps.Count} application(s)?\n\n" +
                "This will create new copies of the selected applications in your Azure AD tenant.",
                "Confirm Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                await RestoreItemsAsync(selectedApps);
            }
        }

        private async void _buttonPanel_BackupAppsClicked(object sender, EventArgs e)
        {
            await BackupAppsAsync();
        }

        private void BackupManager_StatusUpdated(object sender, string message)
        {
            _statusPanel.UpdateStatus(message);
        }

        private void BackupManager_ErrorOccurred(object sender, (string message, bool isError) e)
        {
            _statusPanel.UpdateStatus(e.message, e.isError);
        }

        private void BackupManager_BackupLoaded(object sender, List<GraphApplication> apps)
        {
            _gridManager.LoadApplications(apps);
        }

        private void GridManager_RowCountChanged(object sender, int count)
        {
            _statusPanel.UpdateApplicationCount(count);
        }

        private async void SearchPanel_RefreshClicked(object sender, EventArgs e)
        {
            await RefreshGridAsync();
        }

        private void SearchPanel_SearchTextChanged(object sender, string searchText)
        {
            var graphApps = _apps.Cast<GraphApplication>().ToList();
            _gridManager.FilterApplications(searchText, graphApps);
        }

        private async Task BackupAppsAsync()
        {
            try
            {
                // Get selected apps first to validate
                var selectedApps = _gridManager.GetSelectedApplications();
                if (selectedApps == null || !selectedApps.Any())
                {
                    MessageBox.Show("Please select at least one application to backup.", "No Applications Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string accessToken = null;
                if (chkBackupDefaultClaims.Checked)
                {
                    // Only initiate device code flow if checkbox is checked
                    var deviceCodeResponse = await GetDeviceCodeAsync();
                    if (deviceCodeResponse != null)
                    {
                        accessToken = await PollForTokenAsync(deviceCodeResponse);
                        
                        if (string.IsNullOrEmpty(accessToken))
                        {
                            MessageBox.Show("Failed to acquire access token for default claims backup.", "Authentication Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to initiate device code flow.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                // Proceed with backup using the access token only if checkbox was checked
                await _enterpriseAppsService.BackupApplicationsAsync(selectedApps, accessToken);
                MessageBox.Show("Backup completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during backup: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected List<GraphApplication> GetSelectedItems()
        {
            return _gridManager.GetSelectedApplications();
        }

        protected async Task RestoreItemsAsync(List<GraphApplication> items)
        {
            try
            {
                foreach (var item in items)
                {
                    var backup = new ApplicationBackup 
                    { 
                        Application = item,
                        BackupDate = DateTime.UtcNow,
                        ServicePrincipal = new ServicePrincipal
                        {
                            DisplayName = item.DisplayName,
                            AppId = item.AppId,
                            ServicePrincipalType = "Application",
                            Tags = new List<string> 
                            { 
                                "WindowsAzureActiveDirectoryCustomSingleSignOnApplication",
                                "WindowsAzureActiveDirectoryIntegratedApp"
                            },
                            PreferredSingleSignOnMode = "saml",
                            AppRoleAssignmentRequired = true,
                            LoginUrl = item.Web?.HomePageUrl,
                            NotificationEmailAddresses = new List<string>(),
                            KeyCredentials = new List<KeyCredential>(),
                            PasswordCredentials = new List<PasswordCredential>()
                        },
                        UserAssignments = new List<ServicePrincipalUserAssignment>(),
                        GroupAssignments = new List<ServicePrincipalGroupAssignment>(),
                        SamlConfiguration = new SamlConfiguration
                        {
                            SamlSingleSignOnSettings = new SamlSingleSignOnSettings(),
                            ClaimsMappings = new List<ClaimsMappingPolicy>(),
                            OptionalClaims = item.OptionalClaims ?? new OptionalClaims()
                        }
                    };

                    await _enterpriseAppsService.RestoreApplicationAsync(backup);
                }
                await RefreshGridAsync();
                UpdateStatus("Applications restored successfully");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error restoring applications: {ex.Message}", true);
            }
        }

        public async Task LoadItemsAsync()
        {
            try
            {
                _apps = await _enterpriseAppsService.GetApplicationsAsync();
                _gridManager.LoadApplications(_apps);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading applications: {ex.Message}", true);
            }
        }

        private async Task RefreshGridAsync()
        {
            try
            {
                ShowLoading("Refreshing applications...");
                await LoadItemsAsync();
                HideLoading();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error refreshing applications: {ex.Message}", true);
                HideLoading();
            }
        }

        private void UpdateStatus(string message, bool isError = false)
        {
            _statusPanel.UpdateStatus(message, isError);
        }

        private void ShowLoading(string message)
        {
            _statusPanel.UpdateStatus(message);
            Cursor = Cursors.WaitCursor;
            Enabled = false;
        }

        private void HideLoading()
        {
            Cursor = Cursors.Default;
            Enabled = true;
        }

        private async Task<DeviceCodeResponse> GetDeviceCodeAsync()
        {
            try
            {
                Debug.WriteLine("Starting device code flow...");
                var clientId = "1950a258-227b-4e31-a9cf-717495945fc2"; // Microsoft Azure PowerShell application
                var resource = "https://graph.microsoft.com";
                Debug.WriteLine("Getting tenant ID...");
                var tenantId = await _enterpriseAppsService.GetTenantId();
                Debug.WriteLine($"Got tenant ID: {tenantId}");

                using (var client = new HttpClient())
                {
                    Debug.WriteLine("Sending device code request...");
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("resource", resource),
                        new KeyValuePair<string, string>("client_id", clientId)
                    });

                    var response = await client.PostAsync(
                        $"https://login.microsoftonline.com/{tenantId}/oauth2/devicecode",
                        content
                    );

                    Debug.WriteLine($"Device code response status: {response.StatusCode}");
                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Device code response: {responseContent}");
                        var result = JsonSerializer.Deserialize<DeviceCodeResponse>(responseContent);
                        MessageBox.Show(result.Message, "Device Code Authentication", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return result;
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Error response: {errorContent}");
                        MessageBox.Show($"Failed to get device code: {errorContent}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetDeviceCodeAsync: {ex}");
                MessageBox.Show($"Error getting device code: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private async Task<string> PollForTokenAsync(DeviceCodeResponse deviceCodeResponse)
        {
            try
            {
                Debug.WriteLine("Starting token polling...");
                var clientId = "1950a258-227b-4e31-a9cf-717495945fc2";
                var tenantId = await _enterpriseAppsService.GetTenantId();
                Debug.WriteLine($"Got tenant ID for polling: {tenantId}");

                using (var client = new HttpClient())
                {
                    while (DateTimeOffset.UtcNow < DateTimeOffset.UtcNow.AddSeconds(int.Parse(deviceCodeResponse.ExpiresInString)))
                    {
                        Debug.WriteLine("Sending token request...");
                        var content = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("grant_type", "device_code"),
                            new KeyValuePair<string, string>("code", deviceCodeResponse.DeviceCode),
                            new KeyValuePair<string, string>("client_id", clientId)
                        });

                        var response = await client.PostAsync(
                            $"https://login.microsoftonline.com/{tenantId}/oauth2/token",
                            content
                        );

                        Debug.WriteLine($"Token response status: {response.StatusCode}");
                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            Debug.WriteLine("Successfully got token response");
                            var result = JsonSerializer.Deserialize<TokenResponse>(responseContent);
                            return result.AccessToken;
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            Debug.WriteLine($"Token error response: {errorContent}");
                        }

                        // Wait before polling again
                        Debug.WriteLine("Waiting 5 seconds before next poll...");
                        await Task.Delay(5000);
                    }
                }
                Debug.WriteLine("Token polling timed out");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in PollForTokenAsync: {ex}");
                MessageBox.Show($"Error polling for token: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private class DeviceCodeResponse
        {
            [JsonPropertyName("user_code")]
            public string UserCode { get; set; }

            [JsonPropertyName("device_code")]
            public string DeviceCode { get; set; }

            [JsonPropertyName("verification_url")]
            public string VerificationUrl { get; set; }

            [JsonPropertyName("expires_in")]
            public string ExpiresInString { get; set; }

            [JsonPropertyName("interval")]
            public string IntervalString { get; set; }

            [JsonPropertyName("message")]
            public string Message { get; set; }

            public int ExpiresIn => int.Parse(ExpiresInString);
            public int Interval => int.Parse(IntervalString);
        }

        private class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; }

            [JsonPropertyName("token_type")]
            public string TokenType { get; set; }

            [JsonPropertyName("expires_in")]
            public string ExpiresInString { get; set; }

            [JsonPropertyName("refresh_token")]
            public string RefreshToken { get; set; }

            [JsonPropertyName("scope")]
            public string Scope { get; set; }

            public int ExpiresIn => int.Parse(ExpiresInString);
        }

        private class DeviceCodeResult
        {
            public string Message { get; set; }
            public string DeviceCode { get; set; }
            public DateTimeOffset ExpiresOn { get; set; }
        }

        private void InitializeComponent()
        {
            // Initialize controls
            this.chkBackupDefaultClaims = new CheckBox();
            this.chkBackupDefaultClaims.AutoSize = true;
            this.chkBackupDefaultClaims.Location = new Point(10, 10); // Adjust location as needed
            this.chkBackupDefaultClaims.Name = "chkBackupDefaultClaims";
            this.chkBackupDefaultClaims.Text = "Backup Default User Claims";
            this.Controls.Add(this.chkBackupDefaultClaims);
        }
    }
}
