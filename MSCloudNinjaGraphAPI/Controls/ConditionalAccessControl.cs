using Microsoft.Graph;
using Microsoft.Graph.Models;
using MSCloudNinjaGraphAPI.Services;
using System.Text.Json;
using System.IO;

namespace MSCloudNinjaGraphAPI.Controls
{
    public partial class ConditionalAccessControl : BaseGridControl<ConditionalAccessPolicy>
    {
        private readonly GraphServiceClient _graphClient;
        private readonly ConditionalAccessService _service;
        private List<ConditionalAccessPolicy> _policies;
        private List<ConditionalAccessPolicy> _backupPolicies;
        
        // Left panel controls
        private Panel leftPanel;
        private CheckedListBox lstPolicies;
        private ModernButton btnRefresh;
        private ModernButton btnSelectAll;
        private Label lblBackupStatus;
        
        // Middle panel controls
        private Panel middlePanel;
        private TextBox txtDetails;
        
        // Right panel controls
        private Panel rightPanel;
        private ModernButton btnLoadBackup;
        private ModernButton btnRestoreSelected;
        private ModernButton btnBackupRight;
        private Label lblStatus;
        private OpenFileDialog openFileDialog;
        private FolderBrowserDialog folderBrowserDialog;

        public ConditionalAccessControl(GraphServiceClient graphClient) : base("Conditional Access Policies")
        {
            _graphClient = graphClient;
            _service = new ConditionalAccessService(graphClient);
            InitializeComponent();
            LoadPolicies();
        }

        private void InitializeComponent()
        {
            // Initialize dialogs
            openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Select a backup file"
            };

            folderBrowserDialog = new FolderBrowserDialog
            {
                Description = "Select folder to save backup"
            };

            // Left Panel
            leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 300,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(40, 40, 40)
            };

            lstPolicies = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                CheckOnClick = false,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9)
            };

            lstPolicies.MouseClick += (s, e) => {
                // Get the index of the clicked item
                var index = lstPolicies.IndexFromPoint(e.Location);
                if (index != ListBox.NoMatches)
                {
                    // Get the bounds of the checkbox portion
                    var itemRect = lstPolicies.GetItemRectangle(index);
                    var checkBoxRect = new Rectangle(itemRect.X, itemRect.Y, 16, itemRect.Height);

                    // If click is within checkbox bounds or it's a double click, toggle the check
                    if (checkBoxRect.Contains(e.Location))
                    {
                        lstPolicies.SetItemChecked(index, !lstPolicies.GetItemChecked(index));
                    }
                    
                    // Display policy details regardless of where clicked
                    var selectedPolicy = lstPolicies.Items[index] as ConditionalAccessPolicy;
                    if (selectedPolicy != null)
                    {
                        DisplayPolicyDetails(selectedPolicy);
                    }
                }
            };

            lstPolicies.MouseDoubleClick += (s, e) => {
                var index = lstPolicies.IndexFromPoint(e.Location);
                if (index != ListBox.NoMatches)
                {
                    lstPolicies.SetItemChecked(index, !lstPolicies.GetItemChecked(index));
                }
            };

            btnSelectAll = new ModernButton
            {
                Text = "☑️ Select All",
                Dock = DockStyle.Top,
                Height = 30,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
            btnSelectAll.Click += BtnSelectAll_Click;

            btnRefresh = new ModernButton
            {
                Text = "🔄 Refresh Policies",
                Dock = DockStyle.Top,
                Height = 30,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };

            lblBackupStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "No backup loaded",
                Margin = new Padding(0, 0, 0, 5)
            };

            leftPanel.Controls.AddRange(new Control[] { lstPolicies, btnRefresh, btnSelectAll, lblBackupStatus });
            
            // Middle Panel
            middlePanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(40, 40, 40)
            };

            txtDetails = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10),
                WordWrap = false
            };

            middlePanel.Controls.Add(txtDetails);

            // Right panel
            rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 200,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(40, 40, 40)
            };

            // Create a panel for centered buttons
            var buttonPanel = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.None,
                BackColor = Color.FromArgb(40, 40, 40)
            };

            btnLoadBackup = new ModernButton
            {
                Text = "📂 Load Backup",
                Width = 180,
                Height = 30,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Visible = true
            };
            btnLoadBackup.Click += BtnLoadBackup_Click;

            btnRestoreSelected = new ModernButton
            {
                Text = "♻️ Restore from Backup",
                Width = 180,
                Height = 30,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Visible = true
            };
            btnRestoreSelected.Click += BtnRestore_Click;

            btnBackupRight = new ModernButton
            {
                Text = "💾 Backup Selected",
                Width = 180,
                Height = 30,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Visible = true
            };
            btnBackupRight.Click += BtnBackup_Click;

            lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Text = "Ready",
                ForeColor = Color.White,
                TextAlign = ContentAlignment.BottomLeft
            };

            // Stack buttons vertically in the button panel
            var buttonsTable = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.FromArgb(40, 40, 40)
            };

            buttonsTable.Controls.Add(btnLoadBackup, 0, 0);
            buttonsTable.Controls.Add(btnRestoreSelected, 0, 1);
            buttonsTable.Controls.Add(btnBackupRight, 0, 2);
            buttonPanel.Controls.Add(buttonsTable);

            // Center the button panel in the right panel
            rightPanel.Controls.Add(buttonPanel);
            buttonPanel.Location = new Point(
                (rightPanel.ClientSize.Width - buttonPanel.Width) / 2,
                (rightPanel.ClientSize.Height - buttonPanel.Height) / 2);

            rightPanel.Controls.Add(lblStatus);

            // Add panels to main control
            Controls.Add(rightPanel);
            Controls.Add(middlePanel);
            Controls.Add(leftPanel);

            // Wire up events
            btnRefresh.Click += BtnRefresh_Click;
        }

        private async void LoadPolicies()
        {
            try
            {
                UpdateStatus("Loading policies...");
                _policies = await _service.GetAllPoliciesAsync();
                lstPolicies.Items.Clear();

                foreach (var policy in _policies.OrderBy(p => p.DisplayName))
                {
                    lstPolicies.Items.Add(policy);
                }

                lstPolicies.DisplayMember = "DisplayName";
                UpdateStatus($"Loaded {_policies.Count} policies successfully");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading policies: {ex.Message}", true);
            }
        }

        private async Task DisplayPolicyDetails(ConditionalAccessPolicy policy)
        {
            try
            {
                if (policy != null)
                {
                    txtDetails.Text = await _service.FormatPolicyDetailsAsync(policy);
                    txtDetails.SelectionStart = 0;
                    txtDetails.SelectionLength = 0;
                }
                else
                {
                    txtDetails.Text = "Select a policy to view details";
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error displaying policy details: {ex.Message}", true);
            }
        }

        private void UpdateStatus(string message, bool isError = false)
        {
            lblStatus.ForeColor = isError ? Color.Red : Color.White;
            lblStatus.Text = message;
        }

        private async void BtnRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                await RefreshPolicies();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing policies: {ex.Message}", "Refresh Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnLoadBackup_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    UpdateStatus("Loading backup...");
                    _backupPolicies = await _service.LoadBackupAsync(openFileDialog.FileName);
                    lstPolicies.Items.Clear();

                    foreach (var policy in _backupPolicies.OrderBy(p => p.DisplayName))
                    {
                        lstPolicies.Items.Add(policy);
                    }
                    lstPolicies.DisplayMember = "DisplayName";
                    
                    // Show restore button and update status
                    btnRestoreSelected.Visible = true;
                    lblBackupStatus.Text = $"Backup loaded: {Path.GetFileName(openFileDialog.FileName)}";
                    lblBackupStatus.ForeColor = Color.LightGreen;
                    
                    UpdateStatus($"Loaded {_backupPolicies.Count} policies from backup");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error loading backup: {ex.Message}", true);
                    lblBackupStatus.Text = "Failed to load backup";
                    lblBackupStatus.ForeColor = Color.Red;
                }
            }
        }

        private async void BtnRestore_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedPolicies = GetSelectedItems();
                if (!selectedPolicies.Any())
                {
                    MessageBox.Show("Please select at least one policy to restore.", "No Selection",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var confirmResult = MessageBox.Show(
                    $"Are you sure you want to restore {selectedPolicies.Count} policies? This will update existing policies or create new ones.",
                    "Confirm Restore",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult == DialogResult.Yes)
                {
                    UpdateStatus("Restoring policies...");
                    var restored = 0;
                    var errors = new List<string>();

                    foreach (var policy in selectedPolicies)
                    {
                        try
                        {
                            await _service.RestorePolicyAsync(policy);
                            restored++;
                            UpdateStatus($"Restored {restored}/{selectedPolicies.Count} policies...");
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Failed to restore policy '{policy.DisplayName}': {ex.Message}");
                        }
                    }

                    // Show summary
                    var summary = $"Successfully restored {restored} out of {selectedPolicies.Count} policies.";
                    if (errors.Any())
                    {
                        summary += "\n\nErrors encountered:";
                        foreach (var error in errors)
                        {
                            summary += $"\n- {error}";
                        }
                        MessageBox.Show(summary, "Restore Complete", MessageBoxButtons.OK, 
                            errors.Count == selectedPolicies.Count ? MessageBoxIcon.Error : MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show(summary, "Restore Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    // Switch back to current policies view
                    await RefreshPolicies();
                    btnRestoreSelected.Visible = false;
                    lblBackupStatus.Text = "No backup loaded";
                    lblBackupStatus.ForeColor = Color.White;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error during restore: {ex.Message}", true);
                MessageBox.Show($"An error occurred during restore: {ex.Message}", "Restore Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RefreshPolicies()
        {
            try
            {
                UpdateStatus("Refreshing policies...");
                _policies = await _service.GetAllPoliciesAsync();
                lstPolicies.Items.Clear();

                foreach (var policy in _policies.OrderBy(p => p.DisplayName))
                {
                    lstPolicies.Items.Add(policy);
                }

                lstPolicies.DisplayMember = "DisplayName";
                UpdateStatus($"Loaded {_policies.Count} policies successfully");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error refreshing policies: {ex.Message}", true);
                throw;
            }
        }

        protected override List<ConditionalAccessPolicy> GetSelectedItems()
        {
            return lstPolicies.CheckedItems.Cast<ConditionalAccessPolicy>().ToList();
        }

        protected override Task RestoreItemsAsync(List<ConditionalAccessPolicy> items)
        {
            // Since this is not implemented yet, return a completed task
            UpdateStatus("Restore functionality not implemented for Conditional Access Policies", true);
            return Task.CompletedTask;
        }

        private void BtnSelectAll_Click(object sender, EventArgs e)
        {
            bool anyUnchecked = false;
            for (int i = 0; i < lstPolicies.Items.Count; i++)
            {
                if (!lstPolicies.GetItemChecked(i))
                {
                    anyUnchecked = true;
                    break;
                }
            }

            // If any items are unchecked, check all. Otherwise, uncheck all
            for (int i = 0; i < lstPolicies.Items.Count; i++)
            {
                lstPolicies.SetItemChecked(i, anyUnchecked);
            }

            btnSelectAll.Text = anyUnchecked ? "☐ Unselect All" : "☑️ Select All";
        }

        private async void BtnBackup_Click(object sender, EventArgs e)
        {
            var selectedPolicies = GetSelectedItems();
            if (!selectedPolicies.Any())
            {
                MessageBox.Show("Please select at least one policy to backup.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string fileName = $"ConditionalAccessPolicies_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    string filePath = Path.Combine(folderBrowserDialog.SelectedPath, fileName);
                    
                    string json = JsonSerializer.Serialize(selectedPolicies, new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    });
                    
                    await File.WriteAllTextAsync(filePath, json);
                    UpdateStatus($"Successfully backed up {selectedPolicies.Count} policies to {fileName}");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error during backup: {ex.Message}", true);
                }
            }
        }
    }
}
