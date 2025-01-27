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
        private readonly LogService _logService;
        private List<User> _users;
        private DataGridView usersGrid;
        private Label countLabel;
        private Label statusLabel;
        private ProgressBar progressBar;
        private CheckBox chkDisableUser;
        private CheckBox chkRemoveFromGAL;
        private CheckBox chkRemoveFromGroups;
        private CheckBox chkRemoveLicenses;
        private CheckBox chkUpdateManager;
        private Button btnExecute;
        private BindingSource bindingSource;
        private Panel contentPanel;
        private Panel gridContainer;
        private Panel actionPanel;
        private FlowLayoutPanel searchPanel;
        private TextBox searchBox;
        private Label searchLabel;

        public UserOffboardingControl(GraphServiceClient graphClient)
        {
            _userService = new UserManagementService(graphClient);
            _users = new List<User>();
            bindingSource = new BindingSource();

            InitializeUI();

            // Load users
            LoadUsers();
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.Padding = new Padding(0, 40, 0, 0);

            // Create search panel
            searchPanel = new FlowLayoutPanel
            {
                Height = 40,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(10, 8, 10, 8)
            };

            var searchLabel = new Label
            {
                Text = "Search:",
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(0, 4, 5, 0)
            };

            searchBox = new TextBox
            {
                Width = 300,
                Height = 25,
                Left = 100,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            searchBox.TextChanged += SearchBox_TextChanged;

            searchPanel.Controls.AddRange(new Control[] { searchLabel, searchBox });

            // Create action panel
            actionPanel = new Panel
            {
                Width = 250,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(10)
            };

            var actionsLabel = new Label
            {
                Text = "Actions",
                ForeColor = Color.White,
                AutoSize = true,
                Font = new Font(this.Font.FontFamily, 12, FontStyle.Bold),
                Location = new Point(10, 10)
            };
            actionPanel.Controls.Add(actionsLabel);

            // Create checkboxes
            chkDisableUser = CreateCheckBox("Disable user account", new Point(10, 50));
            chkRemoveFromGAL = CreateCheckBox("Remove from Global Address List", new Point(10, 80));
            chkRemoveFromGroups = CreateCheckBox("Remove from all groups", new Point(10, 110));
            chkRemoveLicenses = CreateCheckBox("Remove all 365 licenses", new Point(10, 140));
            chkUpdateManager = CreateCheckBox("Update manager for direct reports", new Point(10, 170));

            btnExecute = new Button
            {
                Text = "Execute Selected Actions",
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(200, 40),
                Location = new Point(25, 210)
            };
            btnExecute.Click += BtnExecute_Click;

            actionPanel.Controls.AddRange(new Control[]
            {
                actionsLabel,
                chkDisableUser,
                chkRemoveFromGAL,
                chkRemoveFromGroups,
                chkRemoveLicenses,
                chkUpdateManager,
                btnExecute
            });

            // Create status panel
            var statusPanel = new Panel
            {
                Height = 40,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            statusLabel = new Label
            {
                Text = "Ready",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(10, 10)
            };

            countLabel = new Label
            {
                Text = "Users: 0",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(200, 10)
            };

            progressBar = new ProgressBar
            {
                Width = 200,
                Height = 20,
                Location = new Point(400, 10),
                Visible = false
            };

            statusPanel.Controls.AddRange(new Control[] { statusLabel, countLabel, progressBar });

            // Create grid
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

            usersGrid.CellClick += (s, e) =>
            {
                if (e.ColumnIndex == usersGrid.Columns["Selected"].Index && e.RowIndex >= 0)
                {
                    var currentValue = Convert.ToBoolean(usersGrid.Rows[e.RowIndex].Cells["Selected"].Value);
                    usersGrid.Rows[e.RowIndex].Cells["Selected"].Value = !currentValue;
                }
            };
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
                    HeaderText = "Username",
                    DataPropertyName = "UserPrincipalName",
                    Width = 250,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.Automatic
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "Status",
                    HeaderText = "STATUS",
                    DataPropertyName = "AccountEnabled",
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

            usersGrid.CellFormatting += (s, e) =>
            {
                if (e.ColumnIndex == usersGrid.Columns["Status"].Index && e.RowIndex >= 0)
                {
                    var user = usersGrid.Rows[e.RowIndex].DataBoundItem as User;
                    if (user != null)
                    {
                        e.Value = user.AccountEnabled == true ? "Enabled" : "Disabled";
                        e.FormattingApplied = true;
                    }
                }
            };

            // Create main container with padding for spacing
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 40, 0, 0)
            };

            // Add controls to form
            mainContainer.Controls.Add(usersGrid);
            this.Controls.AddRange(new Control[] { searchPanel, actionPanel, statusPanel, mainContainer });

            // Initialize binding source
            bindingSource = new BindingSource();
        }

        public async Task LoadUsers()
        {
            try
            {
                statusLabel.Text = "Loading users...";
                btnExecute.Enabled = false;
                _users = await _userService.GetAllUsersAsync();

                bindingSource.DataSource = _users;
                usersGrid.DataSource = bindingSource;

                // Set all checkboxes to unchecked initially and set status text
                foreach (DataGridViewRow row in usersGrid.Rows)
                {
                    row.Cells["Selected"].Value = false;
                }

                countLabel.Text = $"Users: {_users.Count}";
                statusLabel.Text = "Ready";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading users: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Error loading users";
            }
            finally
            {
                btnExecute.Enabled = true;
            }
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

        private async void BtnExecute_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedUsers = GetSelectedUsers();
                if (!selectedUsers.Any())
                {
                    MessageBox.Show("Please select at least one user.", "No Users Selected",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!chkDisableUser.Checked && !chkRemoveFromGAL.Checked &&
                    !chkRemoveFromGroups.Checked && !chkRemoveLicenses.Checked && !chkUpdateManager.Checked)
                {
                    MessageBox.Show("Please select at least one action to perform.", "No Actions Selected",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var confirmResult = MessageBox.Show(
                    "Are you sure you want to perform the selected actions on the selected users?",
                    "Confirm Actions", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes)
                    return;

                SetControlsEnabled(false);
                progressBar.Visible = true;
                statusLabel.Text = "Executing actions...";

                var totalActions = selectedUsers.Count * (new[] { chkDisableUser.Checked, chkRemoveFromGAL.Checked,
                    chkRemoveFromGroups.Checked, chkRemoveLicenses.Checked, chkUpdateManager.Checked }).Count(x => x);
                var completedActions = 0;

                foreach (var user in selectedUsers)
                {
                    try
                    {
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

                        if (chkRemoveLicenses.Checked)
                        {
                            await _userService.RemoveUserLicensesAsync(user.Id);
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
                    catch (Exception ex)
                    {
                        await _logService.LogAsync($"Error processing user {user.UserPrincipalName}: {ex.Message}", true);
                    }
                }

                await LoadUsers();
                MessageBox.Show("Selected operations completed successfully.", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync($"Error executing actions: {ex.Message}", true);
                MessageBox.Show($"An error occurred: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetControlsEnabled(true);
                progressBar.Visible = false;
                statusLabel.Text = "Ready";
            }
        }

        private List<User> GetSelectedUsers()
        {
            var selectedUsers = new List<User>();
            foreach (DataGridViewRow row in usersGrid.Rows)
            {
                if (Convert.ToBoolean(row.Cells["Selected"].Value))
                {
                    selectedUsers.Add(_users[row.Index]);
                }
            }
            return selectedUsers;
        }

        private void UpdateProgress(int completed, int total)
        {
            var percentage = (int)((float)completed / total * 100);
            statusLabel.Text = $"Progress: {percentage}%";
        }

        private void SetControlsEnabled(bool enabled)
        {
            chkDisableUser.Enabled = enabled;
            chkRemoveFromGAL.Enabled = enabled;
            chkRemoveFromGroups.Enabled = enabled;
            chkRemoveLicenses.Enabled = enabled;
            chkUpdateManager.Enabled = enabled;
            btnExecute.Enabled = enabled;
        }

        private void InitializeComponent()
        {

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
