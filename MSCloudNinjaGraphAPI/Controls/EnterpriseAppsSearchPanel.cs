using System;
using System.Drawing;
using System.Windows.Forms;

namespace MSCloudNinjaGraphAPI.Controls
{
    public class EnterpriseAppsSearchPanel : Panel
    {
        private TextBox searchBox;
        private ModernButton btnRefresh;

        public event EventHandler<string> SearchTextChanged;
        public event EventHandler RefreshClicked;

        public EnterpriseAppsSearchPanel()
        {
            InitializePanel();
            InitializeControls();
        }

        private void InitializePanel()
        {
            Dock = DockStyle.Top;
            Height = 80;
            BackColor = Color.FromArgb(30, 30, 30);
            Padding = new Padding(10);
            Margin = new Padding(0, 0, 0, 10);
        }

        private void InitializeControls()
        {
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
            searchBox.TextChanged += (s, e) => SearchTextChanged?.Invoke(this, searchBox.Text);

            // Create refresh button
            btnRefresh = new ModernButton
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
            btnRefresh.Click += (s, e) => RefreshClicked?.Invoke(this, EventArgs.Empty);
            btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);

            // Add controls
            Controls.Add(searchBox);
            Controls.Add(btnRefresh);
        }

        public string SearchText => searchBox.Text;

        public void EnableControls(bool enable)
        {
            searchBox.Enabled = enable;
            btnRefresh.Enabled = enable;
        }

        public void ClearSearch()
        {
            searchBox.Text = string.Empty;
        }
    }
}
