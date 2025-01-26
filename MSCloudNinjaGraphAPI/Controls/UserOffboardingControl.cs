using Microsoft.Graph;
using Microsoft.Graph.Models;
using MSCloudNinjaGraphAPI.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MSCloudNinjaGraphAPI.Controls
{
    public partial class UserOffboardingControl : UserControl
    {
        private readonly IUserManagementService _userService;
        private List<User> _users;
        private DataGridView usersGrid;
        private Label statusLabel;
        private Label countLabel;
        private Panel actionPanel;
        private Panel searchPanel;
        private TextBox searchBox;
        private CheckBox chkDisableUser;
        private CheckBox chkRemoveFromGAL;
        private CheckBox chkRemoveFromGroups;
        private CheckBox chkUpdateManager;
        private Button btnExecute;
        private BindingSource bindingSource;

        public UserOffboardingControl(GraphServiceClient graphClient)
        {
            _userService = new UserManagementService(graphClient);
            _users = new List<User>();
            bindingSource = new BindingSource();

            // Initialize UI components
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.Padding = new Padding(10, 70, 10, 10); // Add top padding to account for header

            // Create search panel
            searchPanel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 10)
            };

            var searchLabel = new Label
            {
                Text = "Search:",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(10, 15)
            };

            searchBox = new TextBox
            {
                Width = 300,
                Height = 25,
                Location = new Point(70, 12),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            searchBox.TextChanged += SearchBox_TextChanged;

            searchPanel.Controls.AddRange(new Control[] { searchLabel, searchBox });

            // Create status panel at the bottom
            var statusPanel = new Panel
            {
                Height = 40,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(10, 5, 10, 5)
            };

            statusLabel = new Label
            {
                ForeColor = Color.White,
                Text = "Ready",
                AutoSize = true,
                Location = new Point(10, 10)
            };

            countLabel = new Label
            {
                ForeColor = Color.White,
                Text = "Users: 0",
                AutoSize = true,
                Location = new Point(200, 10)
            };

            statusPanel.Controls.AddRange(new Control[] { statusLabel, countLabel });

            // Create main content panel with padding
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 0)
            };

            // Create action panel on the right
            actionPanel = new Panel
            {
                Width = 250,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(10)
            };

            // Create checkboxes for actions
            var actionsLabel = new Label
            {
                Text = "Actions",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(10, 20)
            };

            chkDisableUser = CreateCheckBox("Disable user account", new Point(10, 50));
            chkRemoveFromGAL = CreateCheckBox("Remove from Global Address List", new Point(10, 80));
            chkRemoveFromGroups = CreateCheckBox("Remove from all groups", new Point(10, 110));
            chkUpdateManager = CreateCheckBox("Update manager for direct reports", new Point(10, 140));

            btnExecute = new Button
            {
                Text = "Execute Selected Actions",
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(200, 40),
                Location = new Point(25, 180)
            };
            btnExecute.Click += BtnExecute_Click;

            actionPanel.Controls.AddRange(new Control[] 
            { 
                actionsLabel,
                chkDisableUser, 
                chkRemoveFromGAL, 
                chkRemoveFromGroups, 
                chkUpdateManager,
                btnExecute 
            });

            // Create grid container panel
            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 10, 10)
            };

            // Create users grid
            usersGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                GridColor = Color.FromArgb(50, 50, 50),
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.Single,
                EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                ColumnHeadersHeight = 35,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(45, 45, 48),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI Semibold", 10),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0),
                    SelectionBackColor = Color.FromArgb(45, 45, 48)
                }
            };

            usersGrid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                SelectionBackColor = Color.FromArgb(0, 122, 204),
                SelectionForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Padding = new Padding(5, 0, 0, 0)
            };

            // Enable sorting
            usersGrid.Sorted += UsersGrid_Sorted;

            // Add columns to grid with improved headers
            usersGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewCheckBoxColumn 
                { 
                    Name = "Selected",
                    HeaderText = "",
                    Width = 30,
                    ReadOnly = false,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                },
                new DataGridViewTextBoxColumn 
                { 
                    Name = "DisplayName",
                    HeaderText = "DISPLAY NAME",
                    DataPropertyName = "DisplayName",
                    Width = 200,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.Automatic
                },
                new DataGridViewTextBoxColumn 
                { 
                    Name = "UserPrincipalName",
                    HeaderText = "EMAIL ADDRESS",
                    DataPropertyName = "UserPrincipalName",
                    Width = 250,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.Automatic
                },
                new DataGridViewTextBoxColumn 
                { 
                    Name = "Status",
                    HeaderText = "LOGIN STATUS",
                    Width = 100,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.Automatic
                },
                new DataGridViewTextBoxColumn 
                { 
                    Name = "Department",
                    HeaderText = "DEPARTMENT",
                    DataPropertyName = "Department",
                    Width = 150,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.Automatic
                },
                new DataGridViewTextBoxColumn 
                { 
                    Name = "JobTitle",
                    HeaderText = "JOB TITLE",
                    DataPropertyName = "JobTitle",
                    Width = 150,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.Automatic
                }
            });

            gridContainer.Controls.Add(usersGrid);
            contentPanel.Controls.Add(gridContainer);

            // Add controls to form in the correct order
            this.Controls.Add(contentPanel);
            this.Controls.Add(actionPanel);
            this.Controls.Add(statusPanel);
            this.Controls.Add(searchPanel);

            // Load users
            LoadUsers();
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            if (bindingSource.DataSource == null) return;

            string searchText = searchBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                bindingSource.DataSource = _users;
            }
            else
            {
                var filteredList = _users.Where(u => 
                    (u.DisplayName?.ToLower().Contains(searchText) ?? false) ||
                    (u.UserPrincipalName?.ToLower().Contains(searchText) ?? false) ||
                    (u.Department?.ToLower().Contains(searchText) ?? false) ||
                    (u.JobTitle?.ToLower().Contains(searchText) ?? false)
                ).ToList();

                bindingSource.DataSource = filteredList;
            }

            // Update count
            countLabel.Text = $"Users: {((List<User>)bindingSource.DataSource).Count}";
        }

        private void UsersGrid_Sorted(object sender, EventArgs e)
        {
            // Preserve checkbox states after sorting
            foreach (DataGridViewRow row in usersGrid.Rows)
            {
                var user = row.DataBoundItem as User;
                if (user != null)
                {
                    row.Cells["Selected"].Value = false;
                }
            }
        }

        private async void LoadUsers()
        {
            try
            {
                statusLabel.Text = "Loading users...";
                _users = await _userService.GetAllUsersAsync();
                
                bindingSource.DataSource = _users;
                usersGrid.DataSource = bindingSource;

                // Set all checkboxes to unchecked initially and set status text
                foreach (DataGridViewRow row in usersGrid.Rows)
                {
                    row.Cells["Selected"].Value = false;
                    row.Cells["Status"].Value = ((bool?)row.DataBoundItem.GetType().GetProperty("AccountEnabled")?.GetValue(row.DataBoundItem) ?? false) 
                        ? "Enabled" 
                        : "Disabled";
                }

                countLabel.Text = $"Users: {_users.Count}";
                statusLabel.Text = "Ready";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading users: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Error loading users";
            }
        }

        private async void BtnExecute_Click(object sender, EventArgs e)
        {
            try
            {
                // Get selected users
                var selectedUsers = new List<User>();
                foreach (DataGridViewRow row in usersGrid.Rows)
                {
                    if (Convert.ToBoolean(row.Cells["Selected"].Value))
                    {
                        selectedUsers.Add(_users[row.Index]);
                    }
                }

                if (!selectedUsers.Any())
                {
                    MessageBox.Show("Please select at least one user.", "No Users Selected",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!chkDisableUser.Checked && !chkRemoveFromGAL.Checked && 
                    !chkRemoveFromGroups.Checked && !chkUpdateManager.Checked)
                {
                    MessageBox.Show("Please select at least one action to perform.", "No Actions Selected",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var message = $"Are you sure you want to perform the selected actions on {selectedUsers.Count} user(s)?";
                if (MessageBox.Show(message, "Confirm Actions", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }

                btnExecute.Enabled = false;
                statusLabel.Text = "Executing actions...";

                var totalActions = selectedUsers.Count * (new[] { chkDisableUser.Checked, chkRemoveFromGAL.Checked,
                    chkRemoveFromGroups.Checked, chkUpdateManager.Checked }).Count(x => x);
                var completedActions = 0;

                foreach (var user in selectedUsers)
                {
                    statusLabel.Text = $"Processing user: {user.DisplayName}";

                    if (chkDisableUser.Checked)
                    {
                        await _userService.DisableUserAsync(user.Id);
                        completedActions++;
                        UpdateProgress(completedActions, totalActions);
                    }

                    if (chkRemoveFromGAL.Checked)
                    {
                        await _userService.RemoveFromGlobalAddressListAsync(user.Id);
                        completedActions++;
                        UpdateProgress(completedActions, totalActions);
                    }

                    if (chkRemoveFromGroups.Checked)
                    {
                        await _userService.RemoveFromAllGroupsAsync(user.Id);
                        completedActions++;
                        UpdateProgress(completedActions, totalActions);
                    }

                    if (chkUpdateManager.Checked)
                    {
                        await _userService.UpdateManagerForEmployeesAsync(user.Id);
                        completedActions++;
                        UpdateProgress(completedActions, totalActions);
                    }
                }

                statusLabel.Text = "Actions completed successfully";
                await Task.Delay(1000); // Brief delay to show completion
                LoadUsers(); // Refresh the grid
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing actions: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Error executing actions";
            }
            finally
            {
                btnExecute.Enabled = true;
            }
        }

        private void UpdateProgress(int completed, int total)
        {
            var percentage = (int)((float)completed / total * 100);
            statusLabel.Text = $"Progress: {percentage}%";
        }

        private CheckBox CreateCheckBox(string text, Point location)
        {
            return new CheckBox
            {
                Text = text,
                ForeColor = Color.White,
                AutoSize = true,
                Location = location
            };
        }
    }
}
