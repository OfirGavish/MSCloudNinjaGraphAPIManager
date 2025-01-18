using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using MSCloudNinjaGraphAPI.Services;
using MSCloudNinjaGraphAPI.Controls.Components;
using GraphApplication = Microsoft.Graph.Models.Application;

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

            var selectColumn = new DataGridViewCheckBoxColumn
            {
                Name = "Select",
                HeaderText = "",
                Width = 30
            };
            appsGrid.Columns.Add(selectColumn);

            appsGrid.Columns.Add("Id", "Id");
            appsGrid.Columns.Add("DisplayName", "Display Name");
            appsGrid.Columns.Add("AppId", "App Id");
            appsGrid.Columns.Add("PublisherDomain", "Publisher Domain");
            appsGrid.Columns.Add("SignInAudience", "Sign-in Audience");
            appsGrid.Columns.Add("Description", "Description");
            appsGrid.Columns.Add("Notes", "Notes");
            appsGrid.Columns.Add("IdentifierUris", "Identifier URIs");
            appsGrid.Columns.Add("RequiredResourceAccess", "Required Resource Access");
            appsGrid.Columns.Add("Api", "API");
            appsGrid.Columns.Add("AppRoles", "App Roles");
            appsGrid.Columns.Add("Info", "Info");
            appsGrid.Columns.Add("IsDeviceOnlyAuthSupported", "Device Only Auth");
            appsGrid.Columns.Add("IsFallbackPublicClient", "Fallback Public Client");
            appsGrid.Columns.Add("Tags", "Tags");

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
            if (InvokeRequired)
            {
                Invoke(new Action(() => BackupManager_BackupLoaded(sender, apps)));
                return;
            }

            _apps = apps;
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
                var selectedApps = _gridManager.GetSelectedApplications();
                if (selectedApps.Count == 0)
                {
                    UpdateStatus("No applications selected for backup", true);
                    return;
                }

                await _backupManager.SaveBackupAsync(selectedApps);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error backing up applications: {ex.Message}", true);
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

        private void InitializeComponent()
        {
            // Initialize controls
        }
    }
}
