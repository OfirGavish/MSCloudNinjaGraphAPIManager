using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace MSCloudNinjaGraphAPI.Controls
{
    public partial class EnterpriseAppsControl : BaseGridControl<Microsoft.Graph.Models.Application>
    {
        private readonly GraphServiceClient _graphClient;
        private DataGridView appsGrid;
        private TextBox searchBox;
        private List<Microsoft.Graph.Models.Application> _apps;
        private List<ApplicationBackup> _backupApps;
        private SaveFileDialog saveFileDialog;
        private OpenFileDialog openFileDialog;
        private FolderBrowserDialog folderBrowserDialog;
        private Label lblBackupStatus;
        private Label lblAppCount;

        public EnterpriseAppsControl(GraphServiceClient graphClient) : base("Enterprise Applications")
        {
            _graphClient = graphClient;
            InitializeComponent();
            _ = LoadAppsAsync();
        }

        protected override async Task RestoreItemsAsync(List<Microsoft.Graph.Models.Application> items)
        {
            try
            {
                UpdateStatus("Restoring applications...");
                int restored = 0;
                int errors = 0;

                foreach (var app in items)
                {
                    try
                    {
                        // Check if app exists
                        var existingApp = await _graphClient.Applications
                            .GetAsync(requestConfiguration =>
                            {
                                requestConfiguration.QueryParameters.Filter = $"appId eq '{app.AppId}'";
                                requestConfiguration.QueryParameters.Select = new[] { "id" };
                            });

                        if (existingApp?.Value?.FirstOrDefault() != null)
                        {
                            // Update existing app
                            await _graphClient.Applications[existingApp.Value.First().Id]
                                .PatchAsync(app);
                        }
                        else
                        {
                            // Create new app
                            await _graphClient.Applications
                                .PostAsync(app);
                        }
                        restored++;
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        System.Diagnostics.Debug.WriteLine($"Error restoring app {app.DisplayName}: {ex.Message}");
                    }
                }

                UpdateStatus($"Restored {restored} applications. {errors} failed.");
                if (restored > 0)
                {
                    await LoadAppsAsync(); // Refresh the grid
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error during restore: {ex.Message}", true);
            }
        }

        private void InitializeComponent()
        {
            // Initialize controls
            appsGrid = new DataGridView();
            searchBox = new TextBox();
            lblAppCount = new Label();
            lblBackupStatus = new Label();
            var mainPanel = new Panel();
            var rightPanel = new Panel();
            var topPanel = new Panel();

            // Configure save dialog
            saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                Title = "Save Backup"
            };

            // Configure open dialog
            openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                Title = "Load Backup"
            };

            // Create main panel with padding
            mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10)
            };

            // Create right panel
            rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 200,
                BackColor = Color.FromArgb(40, 40, 40),
                Padding = new Padding(10)
            };

            // Create search panel with proper spacing
            var searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 10)
            };

            // Create search box
            searchBox = new TextBox
            {
                PlaceholderText = "Search applications...",
                Width = 280,
                Height = 30,
                Location = new Point(10, 35),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle
            };
            searchBox.TextChanged += SearchBox_TextChanged;

            // Create refresh button
            var btnRefresh = new Button
            {
                Text = "Refresh",
                Width = 80,
                Height = 30,
                Location = new Point(searchBox.Right + 10, 35),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            btnRefresh.Click += async (s, e) => await RefreshApplications();
            btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);

            // Add controls to search panel
            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(btnRefresh);

            // Create grid panel with AutoScroll
            var gridPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(1),
                AutoScroll = true
            };

            // Configure grid
            appsGrid.BackgroundColor = Color.FromArgb(30, 30, 30);
            appsGrid.ForeColor = Color.White;
            appsGrid.GridColor = Color.FromArgb(50, 50, 50);
            appsGrid.BorderStyle = BorderStyle.None;
            appsGrid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            appsGrid.EnableHeadersVisualStyles = false;
            appsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            appsGrid.MultiSelect = true;
            appsGrid.ReadOnly = false;
            appsGrid.AllowUserToAddRows = false;
            appsGrid.AllowUserToDeleteRows = false;
            appsGrid.AllowUserToResizeRows = false;
            appsGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            appsGrid.RowHeadersVisible = false;
            appsGrid.AutoGenerateColumns = false;
            appsGrid.ScrollBars = ScrollBars.Both;
            appsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            // Configure grid style
            appsGrid.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            appsGrid.DefaultCellStyle.ForeColor = Color.White;
            appsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(60, 60, 60);
            appsGrid.DefaultCellStyle.SelectionForeColor = Color.White;
            appsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(40, 40, 40);
            appsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            appsGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 40, 40);
            appsGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            appsGrid.ColumnHeadersHeight = 30;
            appsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            appsGrid.RowTemplate.Height = 25;

            // Define columns
            var columns = new (string Name, string Header, int Width, Type Type)[]
            {
                ("Select", "", 50, typeof(DataGridViewCheckBoxColumn)),
                ("Id", "ID", 300, typeof(DataGridViewTextBoxColumn)),
                ("DisplayName", "Display Name", 200, typeof(DataGridViewTextBoxColumn)),
                ("AppId", "App ID", 300, typeof(DataGridViewTextBoxColumn)),
                ("PublisherDomain", "Publisher Domain", 200, typeof(DataGridViewTextBoxColumn)),
                ("SignInAudience", "Sign-in Audience", 150, typeof(DataGridViewTextBoxColumn)),
                ("Description", "Description", 300, typeof(DataGridViewTextBoxColumn)),
                ("Notes", "Notes", 200, typeof(DataGridViewTextBoxColumn)),
                ("IdentifierUris", "Identifier URIs", 300, typeof(DataGridViewTextBoxColumn)),
                ("RequiredResourceAccess", "Required Resources", 300, typeof(DataGridViewTextBoxColumn)),
                ("Api", "API Settings", 200, typeof(DataGridViewTextBoxColumn)),
                ("AppRoles", "App Roles", 200, typeof(DataGridViewTextBoxColumn)),
                ("Info", "Info", 200, typeof(DataGridViewTextBoxColumn)),
                ("IsDeviceOnlyAuthSupported", "Device Only Auth", 150, typeof(DataGridViewTextBoxColumn)),
                ("IsFallbackPublicClient", "Fallback Public Client", 150, typeof(DataGridViewTextBoxColumn)),
                ("Tags", "Tags", 200, typeof(DataGridViewTextBoxColumn)),
                ("Certification", "Certification", 200, typeof(DataGridViewTextBoxColumn)),
                ("DisabledByMicrosoftStatus", "Disabled Status", 200, typeof(DataGridViewTextBoxColumn)),
                ("GroupMembershipClaims", "Group Claims", 200, typeof(DataGridViewTextBoxColumn)),
                ("OptionalClaims", "Optional Claims", 200, typeof(DataGridViewTextBoxColumn)),
                ("ParentalControlSettings", "Parental Control", 200, typeof(DataGridViewTextBoxColumn)),
                ("PublicClient", "Public Client", 200, typeof(DataGridViewTextBoxColumn)),
                ("RequestSignatureVerification", "Signature Verification", 200, typeof(DataGridViewTextBoxColumn)),
                ("ServicePrincipalLockConfiguration", "SP Lock Config", 200, typeof(DataGridViewTextBoxColumn)),
                ("TokenEncryptionKeyId", "Token Encryption Key", 300, typeof(DataGridViewTextBoxColumn)),
                ("VerifiedPublisher", "Verified Publisher", 200, typeof(DataGridViewTextBoxColumn)),
                ("DefaultRedirectUri", "Default Redirect URI", 300, typeof(DataGridViewTextBoxColumn))
            };

            // Calculate total width of all columns
            int totalWidth = columns.Sum(col => col.Width);
            
            // Add grid to panel first
            gridPanel.Controls.Add(appsGrid);
            appsGrid.Dock = DockStyle.Fill;
            gridPanel.Dock = DockStyle.Fill;
            
            // Calculate minimum width after adding to panel
            gridPanel.MinimumSize = new Size(800, 400); // Set minimum size for panel
            //appsGrid.MinimumSize = new Size(totalWidth + 50, 400); // Set minimum size for grid
            appsGrid.AutoSize = false;
            
             // Enable smooth horizontal scrolling with mouse wheel
            gridPanel.MouseWheel += (sender, e) =>
            {
                if (ModifierKeys.HasFlag(Keys.Shift))
                {
                    // Scroll horizontally when Shift is pressed
                    int scrollAmount = -e.Delta;
                    gridPanel.HorizontalScroll.Value = Math.Max(0, 
                        Math.Min(gridPanel.HorizontalScroll.Value + scrollAmount,
                        gridPanel.HorizontalScroll.Maximum));
                }
            };

            // Add columns to grid
            foreach (var col in columns)
            {
                var column = (DataGridViewColumn)Activator.CreateInstance(col.Type);
                column.Name = col.Name;
                column.HeaderText = col.Header;
                column.Width = col.Width;
                appsGrid.Columns.Add(column);
            }
            
            // Create labels with theme
            lblAppCount = new Label
            {
                Text = "0 applications",
                AutoSize = true,
                Dock = DockStyle.Bottom,
                ForeColor = Color.White,
                Padding = new Padding(5)
            };

            lblBackupStatus = new Label
            {
                Text = "No backup loaded",
                AutoSize = true,
                Dock = DockStyle.Bottom,
                ForeColor = Color.White,
                Padding = new Padding(5)
            };

            // Create buttons
            var btnSelectAll = new ModernButton
            {
                Text = "✓ Select All",
                Width = 180,
                Height = 30,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Visible = true
            };
            btnSelectAll.Click += (s, e) => SelectAllApps();

            var btnLoadBackup = new ModernButton
            {
                Text = "📂 Load Backup",
                Width = 180,
                Height = 30,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Visible = true
            };
            btnLoadBackup.Click += LoadBackup_Click;

            var btnRestoreApps = new ModernButton
            {
                Text = "♻️ Restore from Backup",
                Width = 180,
                Height = 30,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Visible = true
            };
            btnRestoreApps.Click += RestoreApps_Click;

            var btnBackupApps = new ModernButton
            {
                Text = "💾 Backup Selected",
                Width = 180,
                Height = 30,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Visible = true
            };
            btnBackupApps.Click += BackupApps_Click;

            // Create button panel
            var buttonPanel = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 4,
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = Color.FromArgb(40, 40, 40)
            };

            // Add controls to button panel
            buttonPanel.Controls.Add(btnSelectAll, 0, 0);
            buttonPanel.Controls.Add(btnLoadBackup, 0, 1);
            buttonPanel.Controls.Add(btnRestoreApps, 0, 2);
            buttonPanel.Controls.Add(btnBackupApps, 0, 3);

            // Add controls to panels
            mainPanel.Controls.Add(lblAppCount);
            mainPanel.Controls.Add(gridPanel);
            mainPanel.Controls.Add(searchPanel);

            rightPanel.Controls.Add(buttonPanel);
            rightPanel.Controls.Add(lblBackupStatus);

            // Add panels to main control
            Controls.Add(rightPanel);
            Controls.Add(mainPanel);

            // Set up events
            appsGrid.CellContentClick += AppsGrid_CellContentClick;
            appsGrid.CellValueChanged += AppsGrid_CellValueChanged;
        }

        private void SelectAllApps()
        {
            try
            {
                foreach (DataGridViewRow row in appsGrid.Rows)
                {
                    if (row.Cells.Count > 0 && row.Cells["Select"] is DataGridViewCheckBoxCell checkCell)
                    {
                        checkCell.Value = true;
                    }
                }
                appsGrid.EndEdit();
                appsGrid.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting all items: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            FilterApps(searchBox.Text);
        }

        private async Task LoadAppsAsync()
        {
            try
            {
                UpdateStatus("Loading applications...");
                _apps = new List<Microsoft.Graph.Models.Application>();

                // Initial request
                var apps = await _graphClient.Applications
                    .GetAsync(config =>
                    {
                        config.QueryParameters.Top = 999;
                        config.Headers.Add("ConsistencyLevel", "eventual");
                        config.QueryParameters.Count = true;
                        config.QueryParameters.Select = new[] 
                        { 
                            "id",
                            "appId",
                            "displayName",
                            "publisherDomain",
                            "signInAudience",
                            "createdDateTime",
                            "description",
                            "notes",
                            "identifierUris",
                            "spa",
                            "publicClient",
                            "requiredResourceAccess",
                            "api",
                            "appRoles",
                            "info",
                            "isDeviceOnlyAuthSupported",
                            "isFallbackPublicClient",
                            "tags",
                            "certification",
                            "disabledByMicrosoftStatus",
                            "groupMembershipClaims",
                            "optionalClaims",
                            "parentalControlSettings",
                            "publicClient",
                            "requestSignatureVerification",
                            "servicePrincipalLockConfiguration",
                            "signInAudience",
                            "spa",
                            "tokenEncryptionKeyId",
                            "verifiedPublisher",
                            "defaultRedirectUri"
                        };
                    });

                if (apps?.Value != null)
                {
                    _apps.AddRange(apps.Value);

                    // Continue fetching while there are more pages
                    string nextPageUrl = apps.OdataNextLink;
                    while (!string.IsNullOrEmpty(nextPageUrl))
                    {
                        var nextPageApps = await _graphClient.Applications
                            .GetAsync(config =>
                            {
                                config.QueryParameters.Top = 999;
                                config.Headers.Add("ConsistencyLevel", "eventual");
                                config.QueryParameters.Count = true;
                                config.QueryParameters.Select = new[] 
                                {
                                    "id", "appId", "displayName", "publisherDomain", "signInAudience",
                                    "createdDateTime", "description", "notes", "identifierUris",
                                    "spa", "publicClient", "requiredResourceAccess", "api",
                                    "appRoles", "info", "keyCredentials", "passwordCredentials",
                                    "requiredResourceAccess", "signInAudience", "tags", "groupMembershipClaims",
                                    "optionalClaims", "parentalControlSettings", "publicClient",
                                    "requestSignatureVerification", "servicePrincipalLockConfiguration",
                                    "signInAudience", "spa", "tokenEncryptionKeyId", "verifiedPublisher",
                                    "defaultRedirectUri"
                                };
                            });

                        if (nextPageApps?.Value != null)
                        {
                            _apps.AddRange(nextPageApps.Value);
                            nextPageUrl = nextPageApps.OdataNextLink;
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Clear and populate grid
                    appsGrid.Rows.Clear();
                    var sortedApps = _apps.OrderBy(a => a.DisplayName).ToList();
                    foreach (var app in sortedApps)
                    {
                        AddAppToGrid(app);
                    }

                    if (lblAppCount != null)
                    {
                        lblAppCount.Text = $"Total Applications: {_apps.Count}";
                    }

                    UpdateStatus($"Loaded {_apps.Count} applications");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading applications: {ex.Message}", true);
            }
        }

        private void AddAppToGrid(Microsoft.Graph.Models.Application app)
        {
            int rowIdx = appsGrid.Rows.Add();
            var row = appsGrid.Rows[rowIdx];
            
            row.Cells["Select"].Value = false;
            row.Cells["Id"].Value = app.Id;
            row.Cells["DisplayName"].Value = app.DisplayName;
            row.Cells["AppId"].Value = app.AppId;
            row.Cells["PublisherDomain"].Value = app.PublisherDomain;
            row.Cells["SignInAudience"].Value = app.SignInAudience;
            row.Cells["Description"].Value = app.Description;
            row.Cells["Notes"].Value = app.Notes;
            row.Cells["IdentifierUris"].Value = app.IdentifierUris != null ? string.Join(", ", app.IdentifierUris) : "";
            row.Cells["RequiredResourceAccess"].Value = FormatResourceAccess(app.RequiredResourceAccess);
            row.Cells["Api"].Value = FormatApiSettings(app.Api);
            row.Cells["AppRoles"].Value = FormatAppRoles(app.AppRoles);
            row.Cells["Info"].Value = FormatInfo(app.Info);
            row.Cells["IsDeviceOnlyAuthSupported"].Value = app.IsDeviceOnlyAuthSupported;
            row.Cells["IsFallbackPublicClient"].Value = app.IsFallbackPublicClient;
            row.Cells["Tags"].Value = app.Tags != null ? string.Join(", ", app.Tags) : "";
            row.Cells["Certification"].Value = JsonSerializer.Serialize(app.Certification);
            row.Cells["DisabledByMicrosoftStatus"].Value = app.DisabledByMicrosoftStatus;
            row.Cells["GroupMembershipClaims"].Value = app.GroupMembershipClaims;
            row.Cells["OptionalClaims"].Value = JsonSerializer.Serialize(app.OptionalClaims);
            row.Cells["ParentalControlSettings"].Value = JsonSerializer.Serialize(app.ParentalControlSettings);
            row.Cells["PublicClient"].Value = JsonSerializer.Serialize(app.PublicClient);
            row.Cells["RequestSignatureVerification"].Value = JsonSerializer.Serialize(app.RequestSignatureVerification);
            row.Cells["ServicePrincipalLockConfiguration"].Value = JsonSerializer.Serialize(app.ServicePrincipalLockConfiguration);
            row.Cells["TokenEncryptionKeyId"].Value = app.TokenEncryptionKeyId;
            row.Cells["VerifiedPublisher"].Value = JsonSerializer.Serialize(app.VerifiedPublisher);
            row.Cells["DefaultRedirectUri"].Value = app.DefaultRedirectUri;
        }

        private async void LoadBackup_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string json = await File.ReadAllTextAsync(openFileDialog.FileName);
                    var backups = JsonSerializer.Deserialize<List<ApplicationBackup>>(json);
                    
                    if (backups == null || !backups.Any())
                    {
                        MessageBox.Show("No applications found in backup file.", "Empty Backup",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _backupApps = backups;  // Store the backup for later use
                    lblBackupStatus.Text = $"Backup loaded: {Path.GetFileName(openFileDialog.FileName)}";
                    lblBackupStatus.ForeColor = Color.RoyalBlue;
                    UpdateStatus($"Loaded {backups.Count} applications from backup");

                    // Clear existing applications from the grid
                    appsGrid.Rows.Clear();

                    // Populate the grid with loaded applications
                    foreach (var backup in backups)
                    {
                        AddApplicationToGrid(backup.Application);
                    }
                }
                catch (Exception ex)
                {
                    lblBackupStatus.Text = "Error loading backup";
                    lblBackupStatus.ForeColor = Color.Red;
                    UpdateStatus($"Error loading backup: {ex.Message}", true);
                    MessageBox.Show($"Failed to load backup: {ex.Message}", "Load Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void BackupApps_Click(object sender, EventArgs e)
        {
            var selectedApps = GetSelectedApps();
            if (!selectedApps.Any())
            {
                MessageBox.Show("Please select at least one application to backup.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    UpdateStatus("Creating backup...");
                    var backups = new List<ApplicationBackup>();

                    foreach (var app in selectedApps)
                    {
                        UpdateStatus($"Backing up {app.DisplayName}...");
                        var backup = await GetFullApplicationBackup(app.AppId);
                        if (backup != null)
                        {
                            backups.Add(backup);
                            UpdateStatus($"Successfully backed up {app.DisplayName}");
                        }
                        else
                        {
                            UpdateStatus($"Failed to backup {app.DisplayName}", true);
                        }
                    }

                    if (!backups.Any())
                    {
                        throw new Exception("No applications were successfully backed up");
                    }

                    var options = new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };
                    
                    var json = JsonSerializer.Serialize(backups, options);
                    await File.WriteAllTextAsync(saveFileDialog.FileName, json);
                    UpdateStatus($"Backup created successfully for {backups.Count} applications");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error creating backup: {ex.Message}", true);
                    MessageBox.Show($"Failed to create backup: {ex.Message}", "Backup Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task<ApplicationBackup> GetFullApplicationBackup(string appId)
        {
            try
            {
                UpdateStatus($"Getting application details for {appId}...");
                
                // Get application registration
                var apps = await _graphClient.Applications
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"appId eq '{appId}'";
                        requestConfiguration.QueryParameters.Select = new[] 
                        {
                            "id", "appId", "displayName", "description", "identifierUris",
                            "api", "appRoles", "info", "keyCredentials", "passwordCredentials",
                            "requiredResourceAccess", "signInAudience", "tags", "groupMembershipClaims",
                            "optionalClaims", "parentalControlSettings", "publicClient", "web", "spa"
                        };
                    });

                var app = apps?.Value?.FirstOrDefault();
                if (app == null)
                {
                    UpdateStatus($"Application {appId} not found", true);
                    return null;
                }

                // Get service principal
                var sps = await _graphClient.ServicePrincipals
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"appId eq '{appId}'";
                        requestConfiguration.QueryParameters.Select = new[] 
                        {
                            "id", "appId", "displayName", "description", "notes", "tags",
                            "appRoleAssignmentRequired", "servicePrincipalType", "loginUrl",
                            "preferredTokenSigningKeyThumbprint", "samlSingleSignOnSettings",
                            "keyCredentials", "passwordCredentials"
                        };
                    });

                var sp = sps?.Value?.FirstOrDefault();
                if (sp == null)
                {
                    UpdateStatus($"Service Principal for {appId} not found", true);
                }

                var backup = new ApplicationBackup
                {
                    Application = app,
                    ServicePrincipal = sp,
                    Secrets = app.PasswordCredentials?.ToList() ?? new List<Microsoft.Graph.Models.PasswordCredential>(),
                    Certificates = app.KeyCredentials?.ToList() ?? new List<Microsoft.Graph.Models.KeyCredential>()
                };

                UpdateStatus($"Successfully retrieved backup for {app.DisplayName}");
                return backup;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error getting backup for {appId}: {ex.Message}", true);
                return null;
            }
        }

        private async void RestoreApps_Click(object sender, EventArgs e)
        {
            if (_backupApps == null || !_backupApps.Any())
            {
                MessageBox.Show("Please load a backup file first.", "No Backup Loaded",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Get selected apps from the grid
            var selectedRows = appsGrid.Rows.Cast<DataGridViewRow>()
                .Where(row => Convert.ToBoolean(row.Cells["Select"].Value))
                .ToList();

            if (!selectedRows.Any())
            {
                MessageBox.Show("Please select at least one application to restore.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Find the corresponding backup apps
            var selectedBackups = new List<ApplicationBackup>();
            foreach (var row in selectedRows)
            {
                var appId = row.Cells["AppId"].Value?.ToString();
                var backup = _backupApps.FirstOrDefault(b => b.Application.AppId == appId);
                if (backup != null)
                {
                    selectedBackups.Add(backup);
                }
            }

            var result = MessageBox.Show(
                $"Are you sure you want to restore {selectedBackups.Count} selected applications?",
                "Confirm Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                await RestoreBackupAsync(selectedBackups);
            }
        }

        private async Task RestoreBackupAsync(List<ApplicationBackup> backups)
        {
            int successCount = 0;
            int failureCount = 0;
            var errors = new List<string>();

            UpdateStatus("Starting restore operation...");

            foreach (var backup in backups)
            {
                try
                {
                    // Create application registration first
                    var newApp = new Microsoft.Graph.Models.Application
                    {
                        DisplayName = backup.Application.DisplayName,
                        SignInAudience = backup.Application.SignInAudience,
                        Description = backup.Application.Description,
                        Notes = backup.Application.Notes,
                        Api = backup.Application.Api,
                        AppRoles = backup.Application.AppRoles,
                        Info = backup.Application.Info,
                        IsFallbackPublicClient = backup.Application.IsFallbackPublicClient,
                        IsDeviceOnlyAuthSupported = backup.Application.IsDeviceOnlyAuthSupported,
                        IdentifierUris = backup.Application.IdentifierUris,
                        RequiredResourceAccess = backup.Application.RequiredResourceAccess,
                        Web = backup.Application.Web,
                        Spa = backup.Application.Spa,
                        PublicClient = backup.Application.PublicClient,
                        OptionalClaims = backup.Application.OptionalClaims,
                        ParentalControlSettings = backup.Application.ParentalControlSettings,
                        Tags = backup.Application.Tags
                    };

                    var createdApp = await _graphClient.Applications
                        .PostAsync(newApp);

                    if (createdApp == null)
                    {
                        failureCount++;
                        errors.Add($"Failed to create application registration for {backup.Application.DisplayName}");
                        continue;
                    }

                    // Wait for app registration to propagate
                    await Task.Delay(2000);

                    try
                    {
                        // Create service principal using the app ID from the created application
                        var servicePrincipal = new Microsoft.Graph.Models.ServicePrincipal
                        {
                            AppId = createdApp.AppId, // This is required
                            // Optional properties from backup
                            AccountEnabled = true, // Enable by default
                            DisplayName = backup.ServicePrincipal?.DisplayName ?? backup.Application.DisplayName,
                            Description = backup.ServicePrincipal?.Description ?? backup.Application.Description,
                            Notes = backup.ServicePrincipal?.Notes,
                            LoginUrl = backup.ServicePrincipal?.LoginUrl,
                            AppRoleAssignmentRequired = backup.ServicePrincipal?.AppRoleAssignmentRequired ?? false,
                            Tags = backup.ServicePrincipal?.Tags,
                            ServicePrincipalType = backup.ServicePrincipal?.ServicePrincipalType ?? "Application", // Default to Application type
                            PreferredTokenSigningKeyThumbprint = backup.ServicePrincipal?.PreferredTokenSigningKeyThumbprint,
                            SamlSingleSignOnSettings = backup.ServicePrincipal?.SamlSingleSignOnSettings
                        };

                        var createdSp = await _graphClient.ServicePrincipals
                            .PostAsync(servicePrincipal);

                        if (createdSp != null)
                        {
                            successCount++;
                            UpdateStatus($"Successfully restored {backup.Application.DisplayName}");
                        }
                        else
                        {
                            errors.Add($"Failed to create service principal for {backup.Application.DisplayName} - no error but creation failed");
                        }
                    }
                    catch (Exception spEx)
                    {
                        errors.Add($"Failed to create service principal for {backup.Application.DisplayName}: {spEx.Message}");
                        // Don't increment failure count as the app was created successfully
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    errors.Add($"Failed to restore {backup.Application.DisplayName}: {ex.Message}");
                }
            }

            // Show final status with detailed error report
            var statusMessage = $"Restore operation completed.\nSuccessful: {successCount}\nFailed: {failureCount}";
            if (errors.Any())
            {
                statusMessage += "\n\nErrors encountered:";
                foreach (var error in errors)
                {
                    statusMessage += $"\n- {error}";
                }
                UpdateStatus(statusMessage, true);
            }
            else
            {
                UpdateStatus(statusMessage);
            }

            // Refresh the grid to show current state
            await LoadAppsAsync();
        }

        private async Task<(Microsoft.Graph.Models.Application app, Microsoft.Graph.Models.ServicePrincipal sp)> GetApplicationAndServicePrincipal(string appId)
        {
            try
            {
                // Get the application registration
                var apps = await _graphClient.Applications
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"appId eq '{appId}'";
                    });

                var app = apps?.Value?.FirstOrDefault();
                if (app == null) return (null, null);

                // Get the service principal
                var sps = await _graphClient.ServicePrincipals
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"appId eq '{appId}'";
                    });

                var sp = sps?.Value?.FirstOrDefault();
                return (app, sp);
            }
            catch
            {
                return (null, null);
            }
        }

        private List<Microsoft.Graph.Models.Application> GetSelectedApps()
        {
            var selectedApps = new List<Microsoft.Graph.Models.Application>();
            foreach (DataGridViewRow row in appsGrid.Rows)
            {
                if (row.Cells["Select"].Value is bool isSelected && isSelected)
                {
                    var app = new Microsoft.Graph.Models.Application
                    {
                        Id = row.Cells["Id"].Value?.ToString(),
                        DisplayName = row.Cells["DisplayName"].Value?.ToString(),
                        AppId = row.Cells["AppId"].Value?.ToString(),
                        PublisherDomain = row.Cells["PublisherDomain"].Value?.ToString(),
                        SignInAudience = row.Cells["SignInAudience"].Value?.ToString(),
                        Description = row.Cells["Description"].Value?.ToString(),
                        Notes = row.Cells["Notes"].Value?.ToString(),
                        IdentifierUris = row.Cells["IdentifierUris"].Value?.ToString()?.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                        RequiredResourceAccess = !string.IsNullOrEmpty(row.Cells["RequiredResourceAccess"].Value?.ToString()) 
                            ? JsonSerializer.Deserialize<List<Microsoft.Graph.Models.RequiredResourceAccess>>(row.Cells["RequiredResourceAccess"].Value.ToString())
                            : new List<Microsoft.Graph.Models.RequiredResourceAccess>(),
                        Api = !string.IsNullOrEmpty(row.Cells["Api"].Value?.ToString())
                            ? JsonSerializer.Deserialize<Microsoft.Graph.Models.ApiApplication>(row.Cells["Api"].Value.ToString())
                            : null,
                        AppRoles = !string.IsNullOrEmpty(row.Cells["AppRoles"].Value?.ToString())
                            ? JsonSerializer.Deserialize<List<Microsoft.Graph.Models.AppRole>>(row.Cells["AppRoles"].Value.ToString())
                            : new List<Microsoft.Graph.Models.AppRole>(),
                        Info = !string.IsNullOrEmpty(row.Cells["Info"].Value?.ToString())
                            ? JsonSerializer.Deserialize<Microsoft.Graph.Models.InformationalUrl>(row.Cells["Info"].Value.ToString())
                            : null,
                        IsDeviceOnlyAuthSupported = row.Cells["IsDeviceOnlyAuthSupported"].Value != null 
                            ? Convert.ToBoolean(row.Cells["IsDeviceOnlyAuthSupported"].Value) 
                            : null,
                        IsFallbackPublicClient = row.Cells["IsFallbackPublicClient"].Value != null 
                            ? Convert.ToBoolean(row.Cells["IsFallbackPublicClient"].Value) 
                            : null,
                        Tags = row.Cells["Tags"].Value?.ToString()?.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                        Certification = !string.IsNullOrEmpty(row.Cells["Certification"].Value?.ToString())
                            ? JsonSerializer.Deserialize<Microsoft.Graph.Models.Certification>(row.Cells["Certification"].Value.ToString())
                            : null,
                        DisabledByMicrosoftStatus = row.Cells["DisabledByMicrosoftStatus"].Value?.ToString(),
                        GroupMembershipClaims = row.Cells["GroupMembershipClaims"].Value?.ToString(),
                        OptionalClaims = !string.IsNullOrEmpty(row.Cells["OptionalClaims"].Value?.ToString())
                            ? JsonSerializer.Deserialize<Microsoft.Graph.Models.OptionalClaims>(row.Cells["OptionalClaims"].Value.ToString())
                            : null,
                        ParentalControlSettings = !string.IsNullOrEmpty(row.Cells["ParentalControlSettings"].Value?.ToString())
                            ? JsonSerializer.Deserialize<Microsoft.Graph.Models.ParentalControlSettings>(row.Cells["ParentalControlSettings"].Value.ToString())
                            : null,
                        RequestSignatureVerification = !string.IsNullOrEmpty(row.Cells["RequestSignatureVerification"].Value?.ToString())
                            ? JsonSerializer.Deserialize<Microsoft.Graph.Models.RequestSignatureVerification>(row.Cells["RequestSignatureVerification"].Value.ToString())
                            : null,
                        ServicePrincipalLockConfiguration = !string.IsNullOrEmpty(row.Cells["ServicePrincipalLockConfiguration"].Value?.ToString())
                            ? JsonSerializer.Deserialize<Microsoft.Graph.Models.ServicePrincipalLockConfiguration>(row.Cells["ServicePrincipalLockConfiguration"].Value.ToString())
                            : null,
                        TokenEncryptionKeyId = !string.IsNullOrEmpty(row.Cells["TokenEncryptionKeyId"].Value?.ToString())
                            ? new Guid(row.Cells["TokenEncryptionKeyId"].Value.ToString())
                            : null,
                        VerifiedPublisher = !string.IsNullOrEmpty(row.Cells["VerifiedPublisher"].Value?.ToString())
                            ? JsonSerializer.Deserialize<Microsoft.Graph.Models.VerifiedPublisher>(row.Cells["VerifiedPublisher"].Value.ToString())
                            : null,
                        DefaultRedirectUri = row.Cells["DefaultRedirectUri"].Value?.ToString()
                    };
                    selectedApps.Add(app);
                }
            }
            return selectedApps;
        }

        private string FormatResourceAccess(IList<RequiredResourceAccess> access)
        {
            if (access == null) return "";
            return JsonSerializer.Serialize(access, new JsonSerializerOptions { WriteIndented = true });
        }

        private string FormatApiSettings(ApiApplication api)
        {
            if (api == null) return "";
            return JsonSerializer.Serialize(api, new JsonSerializerOptions { WriteIndented = true });
        }

        private string FormatAppRoles(IList<AppRole> roles)
        {
            if (roles == null) return "";
            return JsonSerializer.Serialize(roles, new JsonSerializerOptions { WriteIndented = true });
        }

        private string FormatInfo(InformationalUrl info)
        {
            if (info == null) return "";
            return JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        }

        private void AppsGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 0 && e.RowIndex >= 0) // Checkbox column
            {
                appsGrid.EndEdit();
            }
        }

        private void AppsGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 0 && e.RowIndex >= 0) // Checkbox column
            {
                appsGrid.InvalidateRow(e.RowIndex);
            }
        }

        private void FilterApps(string searchText)
        {
            foreach (DataGridViewRow row in appsGrid.Rows)
            {
                bool visible = false;
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    visible = true;
                }
                else
                {
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Value?.ToString()?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            visible = true;
                            break;
                        }
                    }
                }
                row.Visible = visible;
            }
        }

        private async Task RefreshApplications()
        {
            try
            {
                UpdateStatus("Refreshing applications list...");
                appsGrid.Rows.Clear();
                await LoadAppsAsync();
                UpdateStatus("Applications list refreshed successfully");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error refreshing applications: {ex.Message}", true);
                MessageBox.Show($"Failed to refresh applications: {ex.Message}", "Refresh Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadApplications()
        {
            try
            {
                var apps = await _graphClient.Applications.GetAsync();
                if (apps?.Value != null)
                {
                    foreach (var app in apps.Value)
                    {
                        AddApplicationToGrid(app);
                    }
                    UpdateAppCount();
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading applications: {ex.Message}", true);
                throw;
            }
        }

        private void AddApplicationToGrid(Microsoft.Graph.Models.Application app)
        {
            int rowIdx = appsGrid.Rows.Add();
            var row = appsGrid.Rows[rowIdx];
            
            row.Cells["Select"].Value = false;
            row.Cells["Id"].Value = app.Id;
            row.Cells["DisplayName"].Value = app.DisplayName;
            row.Cells["AppId"].Value = app.AppId;
            row.Cells["PublisherDomain"].Value = app.PublisherDomain;
            row.Cells["SignInAudience"].Value = app.SignInAudience;
            row.Cells["Description"].Value = app.Description;
            row.Cells["Notes"].Value = app.Notes;
            row.Cells["IdentifierUris"].Value = app.IdentifierUris != null ? string.Join(", ", app.IdentifierUris) : "";
            row.Cells["RequiredResourceAccess"].Value = FormatResourceAccess(app.RequiredResourceAccess);
            row.Cells["Api"].Value = FormatApiSettings(app.Api);
            row.Cells["AppRoles"].Value = FormatAppRoles(app.AppRoles);
            row.Cells["Info"].Value = FormatInfo(app.Info);
            row.Cells["IsDeviceOnlyAuthSupported"].Value = app.IsDeviceOnlyAuthSupported;
            row.Cells["IsFallbackPublicClient"].Value = app.IsFallbackPublicClient;
            row.Cells["Tags"].Value = app.Tags != null ? string.Join(", ", app.Tags) : "";
            row.Cells["Certification"].Value = JsonSerializer.Serialize(app.Certification);
            row.Cells["DisabledByMicrosoftStatus"].Value = app.DisabledByMicrosoftStatus;
            row.Cells["GroupMembershipClaims"].Value = app.GroupMembershipClaims;
            row.Cells["OptionalClaims"].Value = JsonSerializer.Serialize(app.OptionalClaims);
            row.Cells["ParentalControlSettings"].Value = JsonSerializer.Serialize(app.ParentalControlSettings);
            row.Cells["PublicClient"].Value = JsonSerializer.Serialize(app.PublicClient);
            row.Cells["RequestSignatureVerification"].Value = JsonSerializer.Serialize(app.RequestSignatureVerification);
            row.Cells["ServicePrincipalLockConfiguration"].Value = JsonSerializer.Serialize(app.ServicePrincipalLockConfiguration);
            row.Cells["TokenEncryptionKeyId"].Value = app.TokenEncryptionKeyId;
            row.Cells["VerifiedPublisher"].Value = JsonSerializer.Serialize(app.VerifiedPublisher);
            row.Cells["DefaultRedirectUri"].Value = app.DefaultRedirectUri;
        }

        private void UpdateAppCount()
        {
            if (lblAppCount != null)
            {
                lblAppCount.Text = $"Total Applications: {appsGrid.Rows.Count}";
            }
        }

        private void AddAppButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Add application functionality will be implemented soon.", "Coming Soon",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DeleteAppButton_Click(object sender, EventArgs e)
        {
            var selectedApps = GetSelectedItems();
            if (selectedApps.Count == 0)
            {
                UpdateStatus("Please select at least one application to delete.", true);
                return;
            }

            UpdateStatus("Delete application functionality will be implemented soon.", true);
        }

        private void PermissionsButton_Click(object sender, EventArgs e)
        {
            var selectedApps = GetSelectedItems();
            if (selectedApps.Count == 0)
            {
                UpdateStatus("Please select an application to view permissions.", true);
                return;
            }

            if (selectedApps.Count > 1)
            {
                UpdateStatus("Please select only one application to view permissions.", true);
                return;
            }

            UpdateStatus("View permissions functionality will be implemented soon.", true);
        }

        protected override List<Microsoft.Graph.Models.Application> GetSelectedItems()
        {
            var selectedApps = new List<Microsoft.Graph.Models.Application>();
            foreach (DataGridViewRow row in appsGrid.Rows)
            {
                var app = new Microsoft.Graph.Models.Application
                {
                    Id = row.Cells["Id"].Value?.ToString(),
                    DisplayName = row.Cells["DisplayName"].Value?.ToString(),
                    AppId = row.Cells["AppId"].Value?.ToString(),
                    PublisherDomain = row.Cells["PublisherDomain"].Value?.ToString(),
                    SignInAudience = row.Cells["SignInAudience"].Value?.ToString(),
                    Description = row.Cells["Description"].Value?.ToString(),
                    Notes = row.Cells["Notes"].Value?.ToString(),
                    IdentifierUris = row.Cells["IdentifierUris"].Value?.ToString()?.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                    RequiredResourceAccess = !string.IsNullOrEmpty(row.Cells["RequiredResourceAccess"].Value?.ToString()) 
                        ? JsonSerializer.Deserialize<List<Microsoft.Graph.Models.RequiredResourceAccess>>(row.Cells["RequiredResourceAccess"].Value.ToString())
                        : new List<Microsoft.Graph.Models.RequiredResourceAccess>(),
                    Api = !string.IsNullOrEmpty(row.Cells["Api"].Value?.ToString())
                        ? JsonSerializer.Deserialize<Microsoft.Graph.Models.ApiApplication>(row.Cells["Api"].Value.ToString())
                        : null,
                    AppRoles = !string.IsNullOrEmpty(row.Cells["AppRoles"].Value?.ToString())
                        ? JsonSerializer.Deserialize<List<Microsoft.Graph.Models.AppRole>>(row.Cells["AppRoles"].Value.ToString())
                        : new List<Microsoft.Graph.Models.AppRole>(),
                    Info = !string.IsNullOrEmpty(row.Cells["Info"].Value?.ToString())
                        ? JsonSerializer.Deserialize<Microsoft.Graph.Models.InformationalUrl>(row.Cells["Info"].Value.ToString())
                        : null,
                    IsDeviceOnlyAuthSupported = row.Cells["IsDeviceOnlyAuthSupported"].Value != null 
                        ? Convert.ToBoolean(row.Cells["IsDeviceOnlyAuthSupported"].Value) 
                        : null,
                    IsFallbackPublicClient = row.Cells["IsFallbackPublicClient"].Value != null 
                        ? Convert.ToBoolean(row.Cells["IsFallbackPublicClient"].Value) 
                        : null,
                    Tags = row.Cells["Tags"].Value?.ToString()?.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                    Certification = !string.IsNullOrEmpty(row.Cells["Certification"].Value?.ToString())
                        ? JsonSerializer.Deserialize<Microsoft.Graph.Models.Certification>(row.Cells["Certification"].Value.ToString())
                        : null,
                    DisabledByMicrosoftStatus = row.Cells["DisabledByMicrosoftStatus"].Value?.ToString(),
                    GroupMembershipClaims = row.Cells["GroupMembershipClaims"].Value?.ToString(),
                    OptionalClaims = !string.IsNullOrEmpty(row.Cells["OptionalClaims"].Value?.ToString())
                        ? JsonSerializer.Deserialize<Microsoft.Graph.Models.OptionalClaims>(row.Cells["OptionalClaims"].Value.ToString())
                        : null,
                    ParentalControlSettings = !string.IsNullOrEmpty(row.Cells["ParentalControlSettings"].Value?.ToString())
                        ? JsonSerializer.Deserialize<Microsoft.Graph.Models.ParentalControlSettings>(row.Cells["ParentalControlSettings"].Value.ToString())
                        : null,
                    RequestSignatureVerification = !string.IsNullOrEmpty(row.Cells["RequestSignatureVerification"].Value?.ToString())
                        ? JsonSerializer.Deserialize<Microsoft.Graph.Models.RequestSignatureVerification>(row.Cells["RequestSignatureVerification"].Value.ToString())
                        : null,
                    ServicePrincipalLockConfiguration = !string.IsNullOrEmpty(row.Cells["ServicePrincipalLockConfiguration"].Value?.ToString())
                        ? JsonSerializer.Deserialize<Microsoft.Graph.Models.ServicePrincipalLockConfiguration>(row.Cells["ServicePrincipalLockConfiguration"].Value.ToString())
                        : null,
                    TokenEncryptionKeyId = !string.IsNullOrEmpty(row.Cells["TokenEncryptionKeyId"].Value?.ToString())
                        ? new Guid(row.Cells["TokenEncryptionKeyId"].Value.ToString())
                        : null,
                    VerifiedPublisher = !string.IsNullOrEmpty(row.Cells["VerifiedPublisher"].Value?.ToString())
                        ? JsonSerializer.Deserialize<Microsoft.Graph.Models.VerifiedPublisher>(row.Cells["VerifiedPublisher"].Value.ToString())
                        : null,
                    DefaultRedirectUri = row.Cells["DefaultRedirectUri"].Value?.ToString()
                };
                selectedApps.Add(app);
            }
            return selectedApps;
        }

        private void UpdateStatus(string message, bool isError = false)
        {
            if (lblBackupStatus != null)
            {
                lblBackupStatus.Text = message;
                lblBackupStatus.ForeColor = isError ? Color.Red : Color.White;
                
                // For errors, also show a message box
                if (isError)
                {
                    MessageBox.Show(message, "Operation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Custom header cell with checkbox
        public class DataGridViewCheckBoxHeaderCell : DataGridViewColumnHeaderCell
        {
            private bool isChecked = false;
            public event CheckBoxClickedHandler OnCheckBoxClicked;

            public DataGridViewCheckBoxHeaderCell()
            {
            }

            protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, 
                DataGridViewElementStates dataGridViewElementState, object value, object formattedValue, string errorText, 
                DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, 
                DataGridViewPaintParts paintParts)
            {
                base.Paint(graphics, clipBounds, cellBounds, rowIndex, dataGridViewElementState, value, 
                    formattedValue, errorText, cellStyle, advancedBorderStyle, paintParts);

                var checkBoxSize = 15;
                var location = new Point(
                    cellBounds.Location.X + (cellBounds.Width - checkBoxSize) / 2,
                    cellBounds.Location.Y + (cellBounds.Height - checkBoxSize) / 2);
                var checkBoxRect = new Rectangle(location, new Size(checkBoxSize, checkBoxSize));

                ControlPaint.DrawCheckBox(graphics, checkBoxRect, 
                    isChecked ? ButtonState.Checked : ButtonState.Normal);
            }

            protected override void OnMouseClick(DataGridViewCellMouseEventArgs e)
            {
                var checkBoxSize = 15;
                var cellBounds = this.DataGridView.GetCellDisplayRectangle(-1, -1, true);
                var checkBoxRect = new Rectangle(
                    cellBounds.Location.X + (cellBounds.Width - checkBoxSize) / 2,
                    cellBounds.Location.Y + (cellBounds.Height - checkBoxSize) / 2,
                    checkBoxSize, checkBoxSize);

                if (checkBoxRect.Contains(e.Location))
                {
                    isChecked = !isChecked;
                    OnCheckBoxClicked?.Invoke(isChecked);
                    this.DataGridView.InvalidateCell(this);
                }

                base.OnMouseClick(e);
            }
        }

        public delegate void CheckBoxClickedHandler(bool state);
    }
}

public class ApplicationBackup
{
    public Microsoft.Graph.Models.Application Application { get; set; }
    public Microsoft.Graph.Models.ServicePrincipal ServicePrincipal { get; set; }
    public List<Microsoft.Graph.Models.PasswordCredential> Secrets { get; set; }
    public List<Microsoft.Graph.Models.KeyCredential> Certificates { get; set; }
}
