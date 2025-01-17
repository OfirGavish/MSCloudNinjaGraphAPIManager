using System;
using System.Drawing;
using System.Windows.Forms;

namespace MSCloudNinjaGraphAPI.Controls
{
    public class EnterpriseAppsStatusPanel : Panel
    {
        private Label lblAppCount;
        private Label lblStatus;

        public EnterpriseAppsStatusPanel()
        {
            InitializePanel();
            InitializeLabels();
        }

        private void InitializePanel()
        {
            Dock = DockStyle.Bottom;
            Height = 50;
            BackColor = Color.FromArgb(30, 30, 30);
            Padding = new Padding(10);
        }

        private void InitializeLabels()
        {
            // Create app count label
            lblAppCount = new Label
            {
                Text = "0 applications",
                AutoSize = true,
                Dock = DockStyle.Left,
                ForeColor = Color.White,
                Padding = new Padding(5)
            };

            // Create status label
            lblStatus = new Label
            {
                Text = "Ready",
                AutoSize = true,
                Dock = DockStyle.Right,
                ForeColor = Color.White,
                Padding = new Padding(5)
            };

            // Add labels to panel
            Controls.Add(lblAppCount);
            Controls.Add(lblStatus);
        }

        public void UpdateAppCount(int count)
        {
            if (lblAppCount.InvokeRequired)
            {
                lblAppCount.Invoke(new Action(() => UpdateAppCount(count)));
                return;
            }

            lblAppCount.Text = $"Total Applications: {count}";
        }

        public void UpdateApplicationCount(int count)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateApplicationCount(count)));
                return;
            }
            lblAppCount.Text = $"{count} application{(count == 1 ? "" : "s")}";
        }

        public void UpdateStatus(string message, bool isError = false)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(message, isError)));
                return;
            }
            lblStatus.Text = message;
            lblStatus.ForeColor = isError ? Color.Red : Color.White;
        }
    }
}
