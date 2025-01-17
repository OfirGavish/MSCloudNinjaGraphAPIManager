using System.Windows.Forms;
using System.Drawing;

namespace MSCloudNinjaGraphAPI.Controls.Components
{
    public class EnterpriseAppsStatusPanel : Panel
    {
        private readonly Label lblStatus;
        private readonly Label lblAppCount;

        public EnterpriseAppsStatusPanel()
        {
            Dock = DockStyle.Bottom;
            Height = 30;
            Padding = new Padding(5);
            BackColor = Color.FromArgb(45, 45, 48);

            // Create app count label
            lblAppCount = new Label
            {
                Text = "Total Applications: 0",
                Dock = DockStyle.Left,
                AutoSize = true,
                ForeColor = Color.White
            };

            // Create status label
            lblStatus = new Label
            {
                Text = "Ready",
                Dock = DockStyle.Right,
                AutoSize = true,
                ForeColor = Color.White
            };

            Controls.Add(lblAppCount);
            Controls.Add(lblStatus);
        }

        public void UpdateStatus(string message, bool isError = false)
        {
            lblStatus.Text = message;
            lblStatus.ForeColor = isError ? Color.Red : Color.White;
        }

        public void UpdateAppCount(int count)
        {
            lblAppCount.Text = $"Total Applications: {count}";
        }
    }
}
