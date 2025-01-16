using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Http.HttpClientLibrary;
using System.Net.Http;
using System.Net.Http.Headers;

namespace MSCloudNinjaGraphAPI.Controls
{
    public partial class IntuneControl : UserControl
    {
        private readonly GraphServiceClient _graphClient;
        private DataGridView intuneGrid;
        private Label lblItemCount;
        private ModernButton btnBackup;
        private ModernButton btnRestore;
        private ComboBox cmbBackupType;
        private CheckBox chkSelectAll;
        private Label lblStatus;
        private Panel gridPanel;
        private string logFilePath;
        private List<IntuneItem> _items;
        private Dictionary<string, List<object>> backupData;

        // Define backup types and their corresponding endpoints
        private readonly Dictionary<string, string> BackupTypeEndpoints = new Dictionary<string, string>
        {
            { "Device Configuration", "deviceManagement/configurationPolicies" },
            { "Device Compliance", "deviceManagement/compliancePolicies" },
            { "Apps", "deviceAppManagement/mobileApps" },
            { "App Configuration", "deviceAppManagement/targetedManagedAppConfigurations" },
            { "App Protection", "deviceAppManagement/managedAppPolicies" },
            { "Enrollment Configurations", "deviceManagement/deviceEnrollmentConfigurations" }
        };

        public IntuneControl(GraphServiceClient graphClient)
        {
            _graphClient = graphClient;
            _items = new List<IntuneItem>();
            backupData = new Dictionary<string, List<object>>();
            InitializeComponent();
            InitializeLogFile();
            SetupUI();
        }

        private void InitializeLogFile()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MSCloudNinjaGraphAPI");
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            logFilePath = Path.Combine(appDataPath, "intune_control.log");
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

            // Create right sidebar panel
            var sidebarPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 200,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10)
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

            // Create buttons for sidebar with modern styling
            btnBackup = new ModernButton
            {
                Text = "Backup Selected",
                Width = 180,
                Height = 30,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Visible = true,
                Dock = DockStyle.Top
            };
            btnBackup.Click += BtnBackup_Click;

            btnRestore = new ModernButton
            {
                Text = "Restore from Backup",
                Width = 180,
                Height = 30,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Visible = true,
                Dock = DockStyle.Top
            };
            btnRestore.Click += BtnRestore_Click;

            // Create select all checkbox
            chkSelectAll = new CheckBox
            {
                Text = "Select All",
                ForeColor = Color.White,
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 5, 0, 5)
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
                Dock = DockStyle.Top,
                Margin = new Padding(0, 5, 0, 5)
            };

            // Create grid panel
            gridPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30)
            };

            // Add controls to sidebar
            sidebarPanel.Controls.Add(btnRestore);
            sidebarPanel.Controls.Add(btnBackup);
            sidebarPanel.Controls.Add(chkSelectAll);
            sidebarPanel.Controls.Add(lblItemCount);

            // Add controls to top panel
            topPanel.Controls.Add(cmbBackupType);

            // Setup grid
            intuneGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.Black,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = true,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(45, 45, 48),
                RowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(45, 45, 48),
                    ForeColor = Color.White,
                    SelectionBackColor = Color.FromArgb(60, 60, 60),
                    SelectionForeColor = Color.White
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(35, 35, 35),
                    ForeColor = Color.White,
                    SelectionBackColor = Color.FromArgb(35, 35, 35),
                    SelectionForeColor = Color.White,
                    Font = new Font("Segoe UI", 9F, FontStyle.Regular)
                },
                EnableHeadersVisualStyles = false
            };

            SetupDataGrid();

            gridPanel.Controls.Add(intuneGrid);

            // Add panels to main panel in correct order
            mainPanel.Controls.Add(gridPanel);
            mainPanel.Controls.Add(sidebarPanel);
            mainPanel.Controls.Add(lblStatus);
            mainPanel.Controls.Add(topPanel);

            // Add main panel to control
            Controls.Add(mainPanel);

            // Set initial size
            Size = new Size(1000, 600);
        }

        private void SetupDataGrid()
        {
            intuneGrid.AutoGenerateColumns = false;
            intuneGrid.Columns.Clear();

            // Add columns
            var selectColumn = new DataGridViewCheckBoxColumn
            {
                Name = "Select",
                HeaderText = "",
                DataPropertyName = "IsSelected",
                Width = 30
            };
            intuneGrid.Columns.Add(selectColumn);

            intuneGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DisplayName",
                HeaderText = "Name",
                DataPropertyName = "DisplayName",
                Width = 200
            });

            intuneGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Description",
                HeaderText = "Description",
                DataPropertyName = "Description",
                Width = 200
            });

            intuneGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Type",
                HeaderText = "Type",
                DataPropertyName = "Type",
                Width = 150
            });

            intuneGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Version",
                HeaderText = "Version",
                DataPropertyName = "Version",
                Width = 80
            });

            intuneGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "AssignmentCount",
                HeaderText = "Assignments",
                DataPropertyName = "AssignmentCount",
                Width = 100
            });

            intuneGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CreatedDateTime",
                HeaderText = "Created",
                DataPropertyName = "CreatedDateTime",
                Width = 150
            });

            intuneGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LastModifiedDateTime",
                HeaderText = "Modified",
                DataPropertyName = "LastModifiedDateTime",
                Width = 150
            });

            // Configure grid
            intuneGrid.AllowUserToAddRows = false;
            intuneGrid.AllowUserToDeleteRows = false;
            intuneGrid.ReadOnly = false; // Allow checkbox editing
            intuneGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            intuneGrid.MultiSelect = true;
            intuneGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

            // Handle checkbox cell clicks
            intuneGrid.CellContentClick += (s, e) =>
            {
                if (e.ColumnIndex == 0 && e.RowIndex >= 0)
                {
                    var item = _items[e.RowIndex];
                    item.IsSelected = !item.IsSelected;
                    intuneGrid.InvalidateCell(e.ColumnIndex, e.RowIndex);
                }
            };
        }

        private async Task<JObject> GetBetaItemBackupDetails(HttpClient httpClient, string endpoint, string itemId, string type)
        {
            var details = new JObject();

            try
            {
                // Get the main item details
                var response = await httpClient.GetAsync($"https://graph.microsoft.com/beta/{endpoint}/{itemId}");
                response.EnsureSuccessStatusCode(); // This will throw if the response is not successful
                var jsonResponse = await response.Content.ReadAsStringAsync();
                details["policy"] = JObject.Parse(jsonResponse);

                // Get assignments
                response = await httpClient.GetAsync($"https://graph.microsoft.com/beta/{endpoint}/{itemId}/assignments");
                response.EnsureSuccessStatusCode();
                jsonResponse = await response.Content.ReadAsStringAsync();
                details["assignments"] = JObject.Parse(jsonResponse);

                // Get type-specific data needed for restore
                switch (type)
                {
                    case "Device Configuration":
                        // Get only setting values needed for restore
                        response = await httpClient.GetAsync($"https://graph.microsoft.com/beta/{endpoint}/{itemId}/settings");
                        response.EnsureSuccessStatusCode();
                        jsonResponse = await response.Content.ReadAsStringAsync();
                        var settingsObj = JObject.Parse(jsonResponse);
                        // Only keep the value property from each setting
                        var settings = settingsObj["value"] as JArray;
                        if (settings != null)
                        {
                            foreach (JObject setting in settings)
                            {
                                setting.Property("settingDefinitions")?.Remove();
                                setting.Property("@odata.type")?.Remove();
                                setting.Property("id")?.Remove();
                            }
                        }
                        details["settings"] = settingsObj;
                        break;

                    case "App Configuration":
                        // Get only targeted apps list
                        response = await httpClient.GetAsync($"https://graph.microsoft.com/beta/{endpoint}/{itemId}/targetedMobileApps");
                        response.EnsureSuccessStatusCode();
                        jsonResponse = await response.Content.ReadAsStringAsync();
                        details["targetedApps"] = JObject.Parse(jsonResponse);
                        break;

                    case "App Protection":
                        // Get only app settings
                        response = await httpClient.GetAsync($"https://graph.microsoft.com/beta/{endpoint}/{itemId}/apps");
                        response.EnsureSuccessStatusCode();
                        jsonResponse = await response.Content.ReadAsStringAsync();
                        details["apps"] = JObject.Parse(jsonResponse);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error getting backup details for {type} {itemId}: {ex.Message}");
                throw; // Rethrow to handle in the backup method
            }

            return details;
        }

        private async Task<List<IntuneItem>> GetBetaItems(string endpoint, string type)
        {
            var items = new List<IntuneItem>();
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessToken());
            httpClient.DefaultRequestHeaders.Add("ConsistencyLevel", "eventual");

            var betaItems = new List<JToken>();
            string nextLink = $"https://graph.microsoft.com/beta/{endpoint}?$top=999";

            // Get all items with pagination - only basic info
            while (!string.IsNullOrEmpty(nextLink))
            {
                var pageResponse = await httpClient.GetAsync(nextLink);
                pageResponse.EnsureSuccessStatusCode();
                var pageJsonResponse = await pageResponse.Content.ReadAsStringAsync();
                var pageResult = JObject.Parse(pageJsonResponse);
                
                var itemsArray = pageResult["value"] as JArray;
                if (itemsArray != null)
                {
                    betaItems.AddRange(itemsArray);
                    LogMessage($"Found {itemsArray.Count} {type} in current page");
                }

                nextLink = pageResult["@odata.nextLink"]?.ToString();
            }

            LogMessage($"Found total of {betaItems.Count} {type}");
            foreach (var item in betaItems)
            {
                var itemId = item["id"].ToString();
                var name = item["displayName"]?.ToString() ?? item["name"]?.ToString();
                var description = item["description"]?.ToString();
                var createdDateTime = item["createdDateTime"]?.ToString();
                var lastModifiedDateTime = item["lastModifiedDateTime"]?.ToString();
                var version = item["version"]?.ToString();

                if (!string.IsNullOrEmpty(name))
                {
                    items.Add(new IntuneItem
                    {
                        Id = itemId,
                        DisplayName = name,
                        Description = description,
                        CreatedDateTime = createdDateTime,
                        LastModifiedDateTime = lastModifiedDateTime,
                        Version = version,
                        AssignmentCount = 0, // Will be populated during backup
                        Type = type,
                        IsSelected = false
                    });
                }
            }

            return items;
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
                
                // Debug the current authentication state
                try
                {
                    // Test using direct HTTP client first
                    using (var httpClient = new HttpClient())
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/deviceManagement/deviceConfigurations");
                        
                        // Get the token from a test request through the Graph client
                        var testResult = await _graphClient.DeviceManagement.DeviceConfigurations.GetAsync();
                        
                        // Extract the token from the adapter if possible
                        if (_graphClient.RequestAdapter is HttpClientRequestAdapter adapter)
                        {
                            var testRequest = new RequestInformation
                            {
                                HttpMethod = Method.GET,
                                URI = new Uri("https://graph.microsoft.com/v1.0/deviceManagement/deviceConfigurations")
                            };

                            if (adapter.BaseUrl != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Using base URL: {adapter.BaseUrl}");
                            }

                            // Try to get the token from a successful request
                            var authProvider = adapter.GetType().GetField("authProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(adapter) as IAuthenticationProvider;
                            if (authProvider != null)
                            {
                                await authProvider.AuthenticateRequestAsync(testRequest);
                                var authHeader = testRequest.Headers["Authorization"].FirstOrDefault();
                                
                                if (authHeader != null)
                                {
                                    System.Diagnostics.Debug.WriteLine($"IntuneControl using token: {authHeader.Substring(7, 50)}...");
                                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Substring(7));
                                    
                                    var response = await httpClient.SendAsync(request);
                                    System.Diagnostics.Debug.WriteLine($"Direct HTTP test status: {response.StatusCode}");
                                    
                                    if (!response.IsSuccessStatusCode)
                                    {
                                        var error = await response.Content.ReadAsStringAsync();
                                        System.Diagnostics.Debug.WriteLine($"Direct HTTP test error: {error}");
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("No Authorization header found in request!");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("Could not access authentication provider!");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking auth token: {ex.Message}");
                }

                // Clear existing items and grid
                _items.Clear();
                intuneGrid.Rows.Clear();
                
                var selectedType = cmbBackupType.Text;
                var newItems = await GetItemsByType(selectedType);

                // Update the items list with new items
                _items = newItems;

                LogMessage($"Retrieved {_items.Count} items from API");
                intuneGrid.DataSource = _items;

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

        private async Task<List<IntuneItem>> GetItemsByType(string type)
        {
            try
            {
                LogMessage($"Fetching {type}...");
                switch (type)
                {
                    case "Device Configuration":
                        return await GetBetaItems("deviceManagement/configurationPolicies", type);

                    case "Device Compliance":
                        return await GetBetaItems("deviceManagement/compliancePolicies", type);

                    case "Apps":
                        return await GetBetaItems("deviceAppManagement/mobileApps", type);

                    case "App Configuration":
                        return await GetBetaItems("deviceAppManagement/targetedManagedAppConfigurations", type);

                    case "App Protection":
                        return await GetBetaItems("deviceAppManagement/managedAppPolicies", type);

                    default:
                        LogMessage($"Unknown type: {type}", true);
                        return new List<IntuneItem>();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error getting items for type {type}: {ex.Message}", true);
                return new List<IntuneItem>();
            }
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
                var backupData = new Dictionary<string, List<object>>();
                var currentType = cmbBackupType.SelectedItem?.ToString();

                if (string.IsNullOrEmpty(currentType))
                {
                    MessageBox.Show("Please select a backup type first.");
                    return;
                }

                backupData[currentType] = new List<object>();

                var selectedItems = _items.Where(item => item.IsSelected).ToList();
                if (selectedItems.Count == 0)
                {
                    MessageBox.Show("Please select at least one item to backup.");
                    return;
                }

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessToken());
                    httpClient.DefaultRequestHeaders.Add("ConsistencyLevel", "eventual");

                    foreach (var item in selectedItems)
                    {
                        LogMessage($"Backing up {item.Type} - {item.DisplayName}");
                        
                        // Get backup details using the authenticated client
                        var details = await GetBetaItemBackupDetails(httpClient, BackupTypeEndpoints[item.Type], item.Id, item.Type);
                        item.BackupData = details;

                        // Create a backup object that includes both the raw data and assignments
                        var backupObject = new
                        {
                            Data = item.BackupData,
                            Assignments = await GetAssignmentsForItem(item)
                        };
                        backupData[currentType].Add(backupObject);
                    }
                }

                var jsonBackup = JsonConvert.SerializeObject(backupData, Formatting.Indented);

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    FilterIndex = 1,
                    RestoreDirectory = true,
                    FileName = $"IntuneBackup_{currentType}_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(saveFileDialog.FileName, jsonBackup);
                    MessageBox.Show($"Backup completed successfully!\nFile saved to: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error during backup: {ex.Message}", true);
                MessageBox.Show($"Error during backup: {ex.Message}", "Backup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<object> GetAssignmentsForItem(IntuneItem item)
        {
            try
            {
                switch (item.Type)
                {
                    case "Device Configuration":
                        var config = item.BackupData["policy"] as JObject;
                        if (config?.Property("id")?.Value.ToString() != null)
                        {
                            return await _graphClient.DeviceManagement.DeviceConfigurations[config["id"].ToString()].Assignments.GetAsync();
                        }
                        break;

                    case "Device Compliance Policies":
                        var policy = item.BackupData["policy"] as JObject;
                        if (policy?.Property("id")?.Value.ToString() != null)
                        {
                            return await _graphClient.DeviceManagement.DeviceCompliancePolicies[policy["id"].ToString()].Assignments.GetAsync();
                        }
                        break;

                    case "Apps":
                        var app = item.BackupData["policy"] as JObject;
                        if (app?.Property("id")?.Value.ToString() != null)
                        {
                            return await _graphClient.DeviceAppManagement.MobileApps[app["id"].ToString()].Assignments.GetAsync();
                        }
                        break;

                    case "App Configuration Policies":
                        var appConfig = item.BackupData["policy"] as JObject;
                        if (appConfig?.Property("id")?.Value.ToString() != null)
                        {
                            return await _graphClient.DeviceAppManagement.MobileAppConfigurations[appConfig["id"].ToString()].Assignments.GetAsync();
                        }
                        break;

                    case "App Protection Policies":
                        var appPolicy = item.BackupData["policy"] as JObject;
                        if (appPolicy?.Property("id")?.Value.ToString() != null)
                        {
                            // Handle different types of app protection policies
                            if (appPolicy.Property("@odata.type")?.Value.ToString() == "#microsoft.graph.iosManagedAppProtection")
                            {
                                return await _graphClient.DeviceAppManagement.IosManagedAppProtections[appPolicy["id"].ToString()].Assignments.GetAsync();
                            }
                            else if (appPolicy.Property("@odata.type")?.Value.ToString() == "#microsoft.graph.androidManagedAppProtection")
                            {
                                return await _graphClient.DeviceAppManagement.AndroidManagedAppProtections[appPolicy["id"].ToString()].Assignments.GetAsync();
                            }
                        }
                        break;

                    case "Enrollment Configurations":
                        var enrollConfig = item.BackupData["policy"] as JObject;
                        if (enrollConfig?.Property("id")?.Value.ToString() != null)
                        {
                            return await _graphClient.DeviceManagement.DeviceEnrollmentConfigurations[enrollConfig["id"].ToString()].Assignments.GetAsync();
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
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    Title = "Open Backup"
                };

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var json = await File.ReadAllTextAsync(openFileDialog.FileName);
                    backupData = JsonConvert.DeserializeObject<Dictionary<string, List<object>>>(json);

                    var result = MessageBox.Show(
                        $"Are you sure you want to restore {backupData.Values.Sum(list => list.Count)} items from the backup?",
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

        private async Task<string> GetAccessToken()
        {
            var adapter = _graphClient.RequestAdapter as HttpClientRequestAdapter;
            if (adapter != null)
            {
                var authProvider = adapter.GetType().GetField("authProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(adapter) as IAuthenticationProvider;
                if (authProvider != null)
                {
                    var testRequest = new RequestInformation
                    {
                        HttpMethod = Method.GET,
                        URI = new Uri("https://graph.microsoft.com/v1.0/deviceManagement/deviceConfigurations")
                    };
                    await authProvider.AuthenticateRequestAsync(testRequest);
                    var authHeader = testRequest.Headers["Authorization"].FirstOrDefault();
                    return authHeader?.Substring(7); // Remove "Bearer " prefix
                }
            }
            throw new Exception("Could not get access token from graph client");
        }

        private void SetupUI()
        {
            InitializeBackupTypes();
        }

        private void InitializeBackupTypes()
        {
            cmbBackupType.Items.Clear();
            foreach (var type in BackupTypeEndpoints.Keys)
            {
                cmbBackupType.Items.Add(type);
            }
            if (cmbBackupType.Items.Count > 0)
            {
                cmbBackupType.SelectedIndex = 0;
            }
        }

        private async void CmbBackupType_SelectedIndexChanged(object sender, EventArgs e)
        {
            await LoadItemsAsync();
        }

        private void ChkSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            foreach (var item in _items)
            {
                item.IsSelected = chkSelectAll.Checked;
            }
            intuneGrid.Refresh();
        }

        private class IntuneItem
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string CreatedDateTime { get; set; }
            public string LastModifiedDateTime { get; set; }
            public string Version { get; set; }
            public int AssignmentCount { get; set; }
            public string Type { get; set; }
            public bool IsSelected { get; set; }
            public JObject BackupData { get; set; }
        }
    }
}
