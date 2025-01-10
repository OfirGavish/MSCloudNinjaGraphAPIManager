using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Graph.Models.Security;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Net;
using Microsoft.Kiota.Abstractions.Store;

namespace MSCloudNinjaGraphAPI.Controls
{
    public partial class IntuneControl : UserControl
    {
        private readonly GraphServiceClient _graphClient;
        private DataGridView intuneGrid;
        private Label lblItemCount;
        private Button btnBackup;
        private Button btnRestore;
        private ComboBox cmbBackupType;
        private CheckBox chkSelectAll;
        private Label lblStatus;
        private Panel gridPanel;
        private string logFilePath;

        private Dictionary<string, List<object>> backupData;
        private List<IntuneItem> _items;

        // Define backup types and their corresponding endpoints
        private readonly Dictionary<string, string> BackupTypeEndpoints = new Dictionary<string, string>
        {
            { "Device Configuration", "deviceManagement/deviceConfigurations" },
            { "Device Compliance Policies", "deviceManagement/deviceCompliancePolicies" },
            { "Device Security Policies", "deviceManagement/deviceConfigurations?$filter=isof('microsoft.graph.windows10SecureAssessmentConfiguration')" },
            { "Apps", "deviceAppManagement/mobileApps" },
            { "App Configuration Policies", "deviceAppManagement/mobileAppConfigurations" },
            { "App Protection Policies", "deviceAppManagement/managedAppPolicies" },
            { "Enrollment Configurations", "deviceManagement/deviceEnrollmentConfigurations" }
        };

        public IntuneControl (GraphServiceClient graphClient)
        {
            _graphClient = graphClient;
            InitializeComponent();
            InitializeLogging();
            InitializeBackupTypes();
            LogMessage("IntuneControl initialized");
        }

        private void InitializeLogging()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MSCloudNinjaGraphAPI");
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            logFilePath = Path.Combine(appDataPath, "intune_control.log");
            LogMessage("Logging initialized");
        }

        private void LogMessage(string message, bool isError = false)
        {
            try
            {
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {(isError ? "ERROR" : "INFO")} - {message}";
                File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
                System.Diagnostics.Debug.WriteLine($"IntuneControl: {logMessage}");
                UpdateStatus(message, isError);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing to log: {ex.Message}");
            }
        }

        private void InitializeComponent()
        {
            // Create main panel with padding
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10)
            };

            // Create top panel for controls
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(30, 30, 30)
            };

            // Create backup type combo box
            cmbBackupType = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
            cmbBackupType.SelectedIndexChanged += CmbBackupType_SelectedIndexChanged;

            // Create buttons panel (right-aligned)
            var buttonsPanel = new Panel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                BackColor = Color.FromArgb(30, 30, 30)
            };

            // Create buttons
            btnBackup = new Button
            {
                Text = "Backup Selected",
                Width = 120,
                Height = 30,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Margin = new Padding(5),
                Dock = DockStyle.Right
            };
            btnBackup.Click += BtnBackup_Click;

            btnRestore = new Button
            {
                Text = "Restore from Backup",
                Width = 140,
                Height = 30,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Margin = new Padding(5),
                Dock = DockStyle.Right
            };
            btnRestore.Click += BtnRestore_Click;

            // Create select all checkbox
            chkSelectAll = new CheckBox
            {
                Text = "Select All",
                ForeColor = Color.White,
                AutoSize = true,
                Dock = DockStyle.Right,
                Margin = new Padding(5, 10, 5, 0)
            };
            chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;

            // Create status label
            lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(5)
            };

            // Create item count label
            lblItemCount = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Text = "Items: 0",
                Dock = DockStyle.Left,
                Margin = new Padding(10, 10, 0, 0)
            };

            // Create grid panel
            gridPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(10)
            };

            // Configure grid
            intuneGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                GridColor = Color.FromArgb(60, 60, 60),
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false
            };

            intuneGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(60, 60, 60);
            intuneGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            intuneGrid.DefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
            intuneGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(80, 80, 80);
            intuneGrid.DefaultCellStyle.SelectionForeColor = Color.White;

            // Set up columns with specific widths
            var selectColumn = new DataGridViewCheckBoxColumn
            {
                HeaderText = "",
                Name = "Select",
                Width = 30,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };

            var nameColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Name",
                Name = "DisplayName",
                Width = 300,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };

            var typeColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Type",
                Name = "Type",
                Width = 150,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };

            var modifiedColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Last Modified",
                Name = "LastModifiedDateTime",
                Width = 150,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };

            var assignmentColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Assignments",
                Name = "Assignments",
                Width = 200,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };

            var additionalPropsColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Additional Properties",
                Name = "AdditionalProperties",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            intuneGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                selectColumn,
                nameColumn,
                typeColumn,
                modifiedColumn,
                assignmentColumn,
                additionalPropsColumn
            });

            // Set minimum width for the grid panel
            gridPanel.MinimumSize = new Size(1200, 0);

            // Add controls to panels
            buttonsPanel.Controls.Add(btnRestore);
            buttonsPanel.Controls.Add(btnBackup);
            buttonsPanel.Controls.Add(chkSelectAll);

            topPanel.Controls.Add(buttonsPanel);
            topPanel.Controls.Add(lblItemCount);
            topPanel.Controls.Add(cmbBackupType);

            gridPanel.Controls.Add(intuneGrid);

            mainPanel.Controls.Add(gridPanel);
            mainPanel.Controls.Add(lblStatus);
            mainPanel.Controls.Add(topPanel);

            // Add main panel to control
            Controls.Add(mainPanel);

            // Set initial size
            Size = new Size(800, 600);
        }

        private void InitializeBackupTypes()
        {
            cmbBackupType.Items.AddRange(BackupTypeEndpoints.Keys.ToArray());
            if (cmbBackupType.Items.Count > 0)
                cmbBackupType.SelectedIndex = 0;
        }

        private async void CmbBackupType_SelectedIndexChanged(object sender, EventArgs e)
        {
            await LoadItemsAsync();
        }

        private void ChkSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in intuneGrid.Rows)
            {
                row.Cells["Select"].Value = chkSelectAll.Checked;
            }
        }

        private async Task LoadItemsAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(cmbBackupType.Text))
                {
                    LogMessage("No backup type selected");
                    return;
                }

                LogMessage($"Starting to load items for type: {cmbBackupType.Text}");
                intuneGrid.Rows.Clear();
                
                var selectedType = cmbBackupType.Text;
                _items = await GetItemsByType(selectedType);

                LogMessage($"Retrieved {_items.Count} items from API");
                foreach (var item in _items)
                {
                    var row = new DataGridViewRow();
                    row.CreateCells(intuneGrid);
                    row.Cells[0].Value = false; // Select checkbox
                    row.Cells[1].Value = item.DisplayName;
                    row.Cells[2].Value = item.Type;
                    row.Cells[3].Value = item.LastModifiedDateTime?.ToString() ?? "";
                    row.Cells[4].Value = item.Assignments;
                    row.Cells[5].Value = item.AdditionalProperties;
                    intuneGrid.Rows.Add(row);
                }

                lblItemCount.Text = $"Items: {_items.Count}";
                LogMessage($"Successfully loaded {_items.Count} items into grid");
            }
            catch (ODataError ex)
            {
                var errorDetails = ex.Error?.Message ?? "Unknown Graph API error";
                var statusCode = ex.ResponseStatusCode;
                LogMessage($"Graph API Error: {errorDetails} (Status: {statusCode})", true);
                LogMessage($"Error Code: {ex.Error?.Code}", true);
                if (ex.Error?.AdditionalData != null)
                {
                    foreach (var data in ex.Error.AdditionalData)
                    {
                        LogMessage($"Additional Error Data - {data.Key}: {data.Value}", true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading items: {ex.Message}", true);
                LogMessage($"Stack Trace: {ex.StackTrace}", true);
            }
        }

        private async Task<List<T>> GetPaginatedResults<T, TResponse>(Func<Task<TResponse>> getPage) 
            where T : class 
            where TResponse : class
        {
            var items = new List<T>();
            var pageSize = 100;

            try
            {
                var response = await getPage();
                if (response != null)
                {
                    // Handle different response types
                    switch (response)
                    {
                        case DeviceConfigurationCollectionResponse dcr when typeof(T) == typeof(DeviceConfiguration):
                            items.AddRange(dcr.Value?.Cast<T>() ?? Enumerable.Empty<T>());
                            break;
                        case DeviceCompliancePolicyCollectionResponse dcpr when typeof(T) == typeof(DeviceCompliancePolicy):
                            items.AddRange(dcpr.Value?.Cast<T>() ?? Enumerable.Empty<T>());
                            break;
                        case MobileAppCollectionResponse mar when typeof(T) == typeof(MobileApp):
                            items.AddRange(mar.Value?.Cast<T>() ?? Enumerable.Empty<T>());
                            break;
                        case ManagedDeviceMobileAppConfigurationCollectionResponse mdmacr when typeof(T) == typeof(ManagedDeviceMobileAppConfiguration):
                            items.AddRange(mdmacr.Value?.Cast<T>() ?? Enumerable.Empty<T>());
                            break;
                        case ManagedAppPolicyCollectionResponse mapr when typeof(T) == typeof(ManagedAppPolicy):
                            items.AddRange(mapr.Value?.Cast<T>() ?? Enumerable.Empty<T>());
                            break;
                        case DeviceEnrollmentConfigurationCollectionResponse decr when typeof(T) == typeof(DeviceEnrollmentConfiguration):
                            items.AddRange(decr.Value?.Cast<T>() ?? Enumerable.Empty<T>());
                            break;
                    }
                    LogMessage($"Retrieved {items.Count} items");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error in pagination: {ex.Message}", true);
                throw;
            }

            return items;
        }

        private async Task<List<IntuneItem>> GetItemsByType(string type)
        {
            var items = new List<IntuneItem>();
            var maxRetries = 3;
            var currentRetry = 0;

            while (currentRetry < maxRetries)
            {
                try
                {
                    LogMessage($"Fetching {type} from Graph API (Attempt {currentRetry + 1}/{maxRetries})");
                    
                    switch (type)
                    {
                        case "Device Configuration":
                            var configs = await GetPaginatedResults<DeviceConfiguration, DeviceConfigurationCollectionResponse>(async () =>
                            {
                                return await _graphClient.DeviceManagement.DeviceConfigurations
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Top = 100;
                                        requestConfiguration.QueryParameters.Expand = new[] { "assignments" };
                                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                    });
                            });
                            items.AddRange(configs.Select(c => new IntuneItem
                            {
                                Id = c.Id,
                                DisplayName = c.DisplayName,
                                LastModifiedDateTime = c.LastModifiedDateTime,
                                Type = "Device Configuration",
                                Assignments = IntuneItem.FormatAssignments(c),
                                AdditionalProperties = IntuneItem.FormatAdditionalProperties(c),
                                RawData = c
                            }));
                            break;

                        case "Device Compliance Policies":
                            var compliancePolicies = await GetPaginatedResults<DeviceCompliancePolicy, DeviceCompliancePolicyCollectionResponse>(async () =>
                            {
                                return await _graphClient.DeviceManagement.DeviceCompliancePolicies
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Top = 100;
                                        requestConfiguration.QueryParameters.Expand = new[] { "assignments" };
                                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                    });
                            });
                            items.AddRange(compliancePolicies.Select(c => new IntuneItem
                            {
                                Id = c.Id,
                                DisplayName = c.DisplayName,
                                LastModifiedDateTime = c.LastModifiedDateTime,
                                Type = "Device Compliance Policies",
                                Assignments = IntuneItem.FormatAssignments(c),
                                AdditionalProperties = IntuneItem.FormatAdditionalProperties(c),
                                RawData = c
                            }));
                            break;

                        case "Apps":
                            var apps = await GetPaginatedResults<MobileApp, MobileAppCollectionResponse>(async () =>
                            {
                                return await _graphClient.DeviceAppManagement.MobileApps
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Top = 100;
                                        requestConfiguration.QueryParameters.Expand = new[] { "assignments" };
                                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                    });
                            });
                            items.AddRange(apps.Select(c => new IntuneItem
                            {
                                Id = c.Id,
                                DisplayName = c.DisplayName,
                                LastModifiedDateTime = c.LastModifiedDateTime,
                                Type = "Apps",
                                Assignments = IntuneItem.FormatAssignments(c),
                                AdditionalProperties = IntuneItem.FormatAdditionalProperties(c),
                                RawData = c
                            }));
                            break;

                        case "App Configuration Policies":
                            var appConfigPolicies = await GetPaginatedResults<ManagedDeviceMobileAppConfiguration, ManagedDeviceMobileAppConfigurationCollectionResponse>(async () =>
                            {
                                return await _graphClient.DeviceAppManagement.MobileAppConfigurations
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Top = 100;
                                        requestConfiguration.QueryParameters.Expand = new[] { "assignments" };
                                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                    });
                            });
                            items.AddRange(appConfigPolicies.Select(c => new IntuneItem
                            {
                                Id = c.Id,
                                DisplayName = c.DisplayName,
                                LastModifiedDateTime = c.LastModifiedDateTime,
                                Type = "App Configuration Policies",
                                Assignments = IntuneItem.FormatAssignments(c),
                                AdditionalProperties = IntuneItem.FormatAdditionalProperties(c),
                                RawData = c
                            }));
                            break;

                        case "App Protection Policies":
                            var appProtectionPolicies = await GetPaginatedResults<ManagedAppPolicy, ManagedAppPolicyCollectionResponse>(async () =>
                            {
                                return await _graphClient.DeviceAppManagement.ManagedAppPolicies
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Top = 100;
                                        requestConfiguration.QueryParameters.Expand = new[] { "assignments" };
                                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                    });
                            });
                            items.AddRange(appProtectionPolicies.Select(c => new IntuneItem
                            {
                                Id = c.Id,
                                DisplayName = c.DisplayName,
                                LastModifiedDateTime = c.LastModifiedDateTime,
                                Type = "App Protection Policies",
                                Assignments = IntuneItem.FormatAssignments(c),
                                AdditionalProperties = IntuneItem.FormatAdditionalProperties(c),
                                RawData = c
                            }));
                            break;

                        case "Enrollment Configurations":
                            var enrollmentConfigs = await GetPaginatedResults<DeviceEnrollmentConfiguration, DeviceEnrollmentConfigurationCollectionResponse>(async () =>
                            {
                                return await _graphClient.DeviceManagement.DeviceEnrollmentConfigurations
                                    .GetAsync(requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Top = 100;
                                        requestConfiguration.QueryParameters.Expand = new[] { "assignments" };
                                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                                    });
                            });
                            items.AddRange(enrollmentConfigs.Select(c => new IntuneItem
                            {
                                Id = c.Id,
                                DisplayName = c.DisplayName,
                                LastModifiedDateTime = c.LastModifiedDateTime,
                                Type = "Enrollment Configurations",
                                Assignments = IntuneItem.FormatAssignments(c),
                                AdditionalProperties = IntuneItem.FormatAdditionalProperties(c),
                                RawData = c
                            }));
                            break;
                    }
                    
                    return items;
                }
                catch (ODataError ex) when (ex.ResponseStatusCode == 401)
                {
                    LogMessage($"Authentication error on attempt {currentRetry + 1}: {ex.Error?.Message}", true);
                    if (currentRetry == maxRetries - 1)
                        throw new Exception("Failed to authenticate after multiple attempts. Please sign in again.");
                    
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, currentRetry))); // Exponential backoff
                    currentRetry++;
                }
                catch (ODataError ex)
                {
                    LogMessage($"Graph API error: {ex.Error?.Message}", true);
                    throw;
                }
                catch (Exception ex)
                {
                    LogMessage($"Unexpected error: {ex.Message}", true);
                    throw;
                }
            }

            return items;
        }

        private async Task<object> GetFullItemData(string id, string type)
        {
            try
            {
                LogMessage($"Fetching full data for {type} {id}");
                switch (type)
                {
                    case "Device Configuration":
                        LogMessage("Making API call for Device Configuration policy");
                        return await _graphClient.DeviceManagement.DeviceConfigurations[id].GetAsync();

                    case "Device Compliance Policies":
                        LogMessage("Making API call for Device Compliance Policy");
                        return await _graphClient.DeviceManagement.DeviceCompliancePolicies[id].GetAsync();

                    case "Device Security Policies":
                        LogMessage("Making API call for Device Security Policy");
                        return await _graphClient.DeviceManagement.DeviceConfigurations[id].GetAsync();

                    case "Apps":
                        LogMessage("Making API call for App");
                        return await _graphClient.DeviceAppManagement.MobileApps[id].GetAsync();

                    case "App Configuration Policies":
                        LogMessage("Making API call for App Configuration Policy");
                        return await _graphClient.DeviceAppManagement.MobileAppConfigurations[id].GetAsync();

                    case "App Protection Policies":
                        LogMessage("Making API call for App Protection Policy");
                        return await _graphClient.DeviceAppManagement.ManagedAppPolicies[id].GetAsync();

                    case "Enrollment Configurations":
                        LogMessage("Making API call for Enrollment Configuration");
                        return await _graphClient.DeviceManagement.DeviceEnrollmentConfigurations[id].GetAsync();

                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error fetching data for {type} {id}: {ex.Message}", true);
                return null;
            }
        }

        private async void BtnBackup_Click(object sender, EventArgs e)
        {
            await BackupSelectedItems();
        }

        private async Task BackupSelectedItems()
        {
            try
            {
                var selectedRows = intuneGrid.Rows.Cast<DataGridViewRow>()
                    .Where(row => Convert.ToBoolean(row.Cells[0].Value))
                    .ToList();

                if (!selectedRows.Any())
                {
                    MessageBox.Show("Please select at least one item to backup.", "No Selection",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    Title = "Save Backup"
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var backupData = new Dictionary<string, List<object>>();
                    var currentType = cmbBackupType.Text;
                    backupData[currentType] = new List<object>();

                    foreach (var row in selectedRows)
                    {
                        var displayName = row.Cells["DisplayName"].Value.ToString();
                        var type = row.Cells["Type"].Value.ToString();
                        
                        // Find the item in our items list
                        var item = _items.FirstOrDefault(i => i.DisplayName == displayName && i.Type == type);
                        if (item?.RawData != null)
                        {
                            // Create a backup object that includes both the raw data and assignments
                            var backupObject = new
                            {
                                Data = item.RawData,
                                Assignments = await GetAssignmentsForItem(item)
                            };
                            backupData[currentType].Add(backupObject);
                        }
                    }

                    var jsonSettings = new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        NullValueHandling = NullValueHandling.Include,
                        Formatting = Formatting.Indented
                    };

                    var json = JsonConvert.SerializeObject(backupData, jsonSettings);
                    await File.WriteAllTextAsync(saveFileDialog.FileName, json);

                    MessageBox.Show($"Successfully backed up {selectedRows.Count} items to {saveFileDialog.FileName}",
                        "Backup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during backup: {ex.Message}", "Backup Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<object> GetAssignmentsForItem(IntuneItem item)
        {
            try
            {
                switch (item.Type)
                {
                    case "Device Configuration":
                        var config = item.RawData as DeviceConfiguration;
                        if (config?.Id != null)
                        {
                            return await _graphClient.DeviceManagement.DeviceConfigurations[config.Id].Assignments
                                .GetAsync();
                        }
                        break;

                    case "Device Compliance Policies":
                        var policy = item.RawData as DeviceCompliancePolicy;
                        if (policy?.Id != null)
                        {
                            return await _graphClient.DeviceManagement.DeviceCompliancePolicies[policy.Id].Assignments
                                .GetAsync();
                        }
                        break;

                    case "Apps":
                        var app = item.RawData as MobileApp;
                        if (app?.Id != null)
                        {
                            return await _graphClient.DeviceAppManagement.MobileApps[app.Id].Assignments
                                .GetAsync();
                        }
                        break;

                    case "App Configuration Policies":
                        var appConfig = item.RawData as ManagedDeviceMobileAppConfiguration;
                        if (appConfig?.Id != null)
                        {
                            return await _graphClient.DeviceAppManagement.MobileAppConfigurations[appConfig.Id].Assignments
                                .GetAsync();
                        }
                        break;

                    case "App Protection Policies":
                        var appPolicy = item.RawData as ManagedAppPolicy;
                        if (appPolicy?.Id != null)
                        {
                            // Handle different types of app protection policies
                            if (appPolicy is IosManagedAppProtection)
                            {
                                return await _graphClient.DeviceAppManagement.IosManagedAppProtections[appPolicy.Id].Assignments
                                    .GetAsync();
                            }
                            else if (appPolicy is AndroidManagedAppProtection)
                            {
                                return await _graphClient.DeviceAppManagement.AndroidManagedAppProtections[appPolicy.Id].Assignments
                                    .GetAsync();
                            }
                        }
                        break;

                    case "Enrollment Configurations":
                        var enrollConfig = item.RawData as DeviceEnrollmentConfiguration;
                        if (enrollConfig?.Id != null)
                        {
                            return await _graphClient.DeviceManagement.DeviceEnrollmentConfigurations[enrollConfig.Id].Assignments
                                .GetAsync();
                        }
                        break;
                }
                return null;
            }
            catch (Exception ex)
            {
                LogMessage($"Error getting assignments for {item.DisplayName}: {ex.Message}", true);
                return null;
            }
        }

        private async void BtnRestore_Click(object sender, EventArgs e)
        {
            try
            {
                using var dialog = new OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    Title = "Select Intune Backup File"
                };

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    LogMessage("Loading backup file...");
                    var json = await File.ReadAllTextAsync(dialog.FileName);
                    backupData = JsonConvert.DeserializeObject<Dictionary<string, List<object>>>(json);

                    if (backupData == null || !backupData.Any())
                    {
                        MessageBox.Show("The backup file is empty or invalid.", "Invalid Backup",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var result = MessageBox.Show(
                        "Are you sure you want to restore the selected items? This action cannot be undone.",
                        "Confirm Restore",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.Yes)
                    {
                        await RestoreFromBackup();
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error during restore: {ex.Message}", true);
            }
        }

        private async Task RestoreFromBackup()
        {
            int successCount = 0;
            int failureCount = 0;
            var errors = new List<string>();

            foreach (var type in backupData.Keys)
            {
                foreach (var item in backupData[type])
                {
                    try
                    {
                        await RestoreItem(type, item);
                        successCount++;
                        LogMessage($"Restored {type} item successfully");
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        errors.Add($"Failed to restore {type} item: {ex.Message}");
                    }
                }
            }

            var statusMessage = $"Restore operation completed.\nSuccessful: {successCount}\nFailed: {failureCount}";
            if (errors.Any())
            {
                statusMessage += "\n\nErrors encountered:";
                foreach (var error in errors)
                {
                    statusMessage += $"\n- {error}";
                }
            }
            LogMessage(statusMessage);

            // Refresh the grid
            await LoadItemsAsync();
        }

        private async Task RestoreItem(string type, object item)
        {
            switch (type)
            {
                case "Device Configuration":
                    LogMessage("Restoring Device Configuration policy");
                    await _graphClient.DeviceManagement.DeviceConfigurations.PostAsync(((dynamic)item).Data);
                    break;

                case "Device Compliance Policies":
                    LogMessage("Restoring Device Compliance Policy");
                    await _graphClient.DeviceManagement.DeviceCompliancePolicies.PostAsync(((dynamic)item).Data);
                    break;

                case "Device Security Policies":
                    LogMessage("Restoring Device Security Policy");
                    await _graphClient.DeviceManagement.DeviceConfigurations.PostAsync(((dynamic)item).Data);
                    break;

                case "Apps":
                    LogMessage("Restoring App");
                    await _graphClient.DeviceAppManagement.MobileApps.PostAsync(((dynamic)item).Data);
                    break;

                case "App Configuration Policies":
                    LogMessage("Restoring App Configuration Policy");
                    await _graphClient.DeviceAppManagement.MobileAppConfigurations.PostAsync(((dynamic)item).Data);
                    break;

                case "App Protection Policies":
                    LogMessage("Restoring App Protection Policy");
                    await _graphClient.DeviceAppManagement.ManagedAppPolicies.PostAsync(((dynamic)item).Data);
                    break;

                case "Enrollment Configurations":
                    LogMessage("Restoring Enrollment Configuration");
                    await _graphClient.DeviceAppManagement.MobileAppConfigurations.PostAsync(((dynamic)item).Data);
                    break;

                default:
                    throw new NotSupportedException($"Restore not implemented for type: {type}");
            }
        }

        private void UpdateStatus(string message, bool isError = false)
        {
            lblStatus.ForeColor = isError ? Color.Red : Color.White;
            lblStatus.Text = message;
        }

        private class GraphResponse
        {
            public List<dynamic> Value { get; set; }
        }

        private class IntuneItem
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public DateTimeOffset? LastModifiedDateTime { get; set; }
            public object RawData { get; set; }
            public string Type { get; set; }
            public string Assignments { get; set; }
            public string AdditionalProperties { get; set; }

            public static string FormatAdditionalProperties(object item)
            {
                try
                {
                    switch (item)
                    {
                        case DeviceConfiguration config:
                            var platformType = config.GetType().GetProperty("PlatformType")?.GetValue(config)?.ToString() ?? "Not specified";
                            return $"Platform: {platformType}, " +
                                   $"Version: {(config.Version?.ToString() ?? "1.0")}, " +
                                   $"Settings Count: {GetSettingsCount(config)}";

                        case DeviceCompliancePolicy policy:
                            var policyPlatform = policy.GetType().GetProperty("PlatformType")?.GetValue(policy)?.ToString() ?? "Not specified";
                            return $"Platform: {policyPlatform}, " +
                                   $"Version: {(policy.Version?.ToString() ?? "1.0")}, " +
                                   $"Schedule: {policy.ScheduledActionsForRule?.FirstOrDefault()?.ScheduledActionConfigurations?.FirstOrDefault()?.GracePeriodHours ?? 0}h grace period";

                        case MobileApp app:
                            var version = "";
                            if (app is MobileLobApp lobApp)
                            {
                                version = lobApp.GetType().GetProperty("Version")?.GetValue(lobApp)?.ToString() ?? "N/A";
                            }
                            return $"Publisher: {app.Publisher ?? "Not specified"}, " +
                                   $"Type: {app.OdataType?.Replace("#microsoft.graph.", "") ?? "Unknown"}, " +
                                   $"Version: {version}";

                        case ManagedDeviceMobileAppConfiguration appConfig:
                            var settingsCount = appConfig.GetType().GetProperty("Settings")?.GetValue(appConfig) as IEnumerable<object>;
                            return $"Target Platform: {appConfig.TargetedMobileApps?.Count ?? 0} apps, " +
                                   $"Settings Count: {settingsCount?.Count() ?? 0}";

                        case ManagedAppPolicy appPolicy:
                            if (appPolicy is IosManagedAppProtection protection)
                            {
                                var storageLocations = protection.GetType().GetProperty("AllowedDataStorageLocations")?.GetValue(protection) as IEnumerable<object>;
                                var pinRequired = protection.GetType().GetProperty("PinRequired")?.GetValue(protection) as bool? ?? false;
                                return $"Allowed Data Storage: {(storageLocations != null ? string.Join(", ", storageLocations) : "None")}, " +
                                       $"PIN Required: {pinRequired}";
                            }
                            return "No additional properties available";

                        case DeviceEnrollmentConfiguration enrollConfig:
                            var platformRestrictions = enrollConfig.GetType().GetProperty("PlatformRestrictions")?.GetValue(enrollConfig);
                            var platformBlocked = platformRestrictions?.GetType().GetProperty("PlatformBlocked")?.GetValue(platformRestrictions) as bool? ?? false;
                            return $"Priority: {enrollConfig.Priority?.ToString() ?? "0"}, " +
                                   $"Platform Blocked: {platformBlocked}";

                        default:
                            return "No additional properties available";
                    }
                }
                catch (Exception ex)
                {
                    return $"Error getting properties: {ex.Message}";
                }
            }

            private static int GetSettingsCount(DeviceConfiguration config)
            {
                // Count all non-null properties that represent settings
                var settingsProperties = config.GetType().GetProperties()
                    .Where(p => !new[] { "Id", "DisplayName", "Description", "Version", "LastModifiedDateTime", "CreatedDateTime", "PlatformType" }
                        .Contains(p.Name))
                    .Count(p => p.GetValue(config) != null);

                return settingsProperties;
            }

            public static string FormatAssignments<T>(T item) where T : Entity
            {
                try
                {
                    var assignments = item.GetType().GetProperty("Assignments")?.GetValue(item) as IEnumerable<object>;
                    if (assignments == null) return "No assignments";

                    var groups = assignments.Select(a => 
                    {
                        var intent = a.GetType().GetProperty("Intent")?.GetValue(a)?.ToString() ?? "Unknown";
                        var target = a.GetType().GetProperty("Target")?.GetValue(a);
                        var targetType = target?.GetType().GetProperty("OdataType")?.GetValue(target)?.ToString() ?? "";
                        var groupId = target?.GetType().GetProperty("GroupId")?.GetValue(target)?.ToString() ?? "";
                        
                        return $"{intent}: {targetType.Replace("#microsoft.graph.", "")} {(string.IsNullOrEmpty(groupId) ? "" : $"({groupId})")}";
                    });

                    return string.Join(", ", groups);
                }
                catch
                {
                    return "Error reading assignments";
                }
            }
        }
    }
}
