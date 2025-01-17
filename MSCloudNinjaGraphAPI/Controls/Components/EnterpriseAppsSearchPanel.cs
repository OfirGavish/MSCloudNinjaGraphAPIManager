using System;
using System.Windows.Forms;
using System.Drawing;

namespace MSCloudNinjaGraphAPI.Controls.Components
{
    public class EnterpriseAppsSearchPanel : Panel
    {
        public event EventHandler<string> SearchTextChanged;
        public event EventHandler RefreshClicked;

        private readonly TextBox searchBox;
        private readonly Button btnRefresh;

        public EnterpriseAppsSearchPanel()
        {
            Dock = DockStyle.Top;
            Height = 40;
            Padding = new Padding(5);

            // Create search box
            searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Search applications..."
            };

            // Create refresh button
            btnRefresh = new Button
            {
                Text = "Refresh",
                Dock = DockStyle.Right,
                Width = 80,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            searchBox.TextChanged += (s, e) => SearchTextChanged?.Invoke(this, searchBox.Text);
            btnRefresh.Click += (s, e) => RefreshClicked?.Invoke(this, EventArgs.Empty);

            Controls.Add(searchBox);
            Controls.Add(btnRefresh);
        }
    }
}
