using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace MSCloudNinjaGraphAPI.Controls
{
    public static class EnterpriseAppsGridConfiguration
    {
        public static readonly (string Name, string Header, int Width, Type Type)[] Columns = new[]
        {
            ("Select", "", 30, typeof(DataGridViewCheckBoxColumn)),
            ("DisplayName", "Display Name", 200, typeof(DataGridViewTextBoxColumn)),
            ("AppId", "App ID", 300, typeof(DataGridViewTextBoxColumn)),
            ("PublisherDomain", "Publisher Domain", 200, typeof(DataGridViewTextBoxColumn)),
            ("SignInAudience", "Sign-in Audience", 150, typeof(DataGridViewTextBoxColumn)),
            ("Description", "Description", 300, typeof(DataGridViewTextBoxColumn)),
            ("Notes", "Notes", 200, typeof(DataGridViewTextBoxColumn)),
            ("RequiredResourceAccess", "Required Resources", 300, typeof(DataGridViewTextBoxColumn)),
            ("Api", "API Settings", 200, typeof(DataGridViewTextBoxColumn)),
            ("AppRoles", "App Roles", 200, typeof(DataGridViewTextBoxColumn)),
            ("Info", "Info", 200, typeof(DataGridViewTextBoxColumn))
        };

        public static void ConfigureGrid(DataGridView grid)
        {
            // Basic configuration
            grid.BackgroundColor = Color.FromArgb(30, 30, 30);
            grid.ForeColor = Color.White;
            grid.GridColor = Color.FromArgb(50, 50, 50);
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            grid.EnableHeadersVisualStyles = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.ReadOnly = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowHeadersVisible = false;
            grid.AutoGenerateColumns = false;
            grid.ScrollBars = ScrollBars.Both;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            // Style configuration
            grid.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            grid.DefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(60, 60, 60);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(40, 40, 40);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 40, 40);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            grid.ColumnHeadersHeight = 30;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowTemplate.Height = 25;

            // Add columns
            foreach (var col in Columns)
            {
                var column = (DataGridViewColumn)Activator.CreateInstance(col.Type);
                column.Name = col.Name;
                column.HeaderText = col.Header;
                column.Width = col.Width;
                grid.Columns.Add(column);
            }
        }

        public static int GetTotalColumnsWidth()
        {
            return Columns.Sum(col => col.Width);
        }
    }
}