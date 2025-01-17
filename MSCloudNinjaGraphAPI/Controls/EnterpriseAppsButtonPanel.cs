using System;
using System.Drawing;
using System.Windows.Forms;

namespace MSCloudNinjaGraphAPI.Controls
{
    public class EnterpriseAppsButtonPanel : TableLayoutPanel
    {
        private readonly ModernButton btnSelectAll;
        private readonly ModernButton btnLoadBackup;
        private readonly ModernButton btnRestoreApps;
        private readonly ModernButton btnBackupApps;

        public event EventHandler SelectAllClicked;
        public event EventHandler LoadBackupClicked;
        public event EventHandler RestoreAppsClicked;
        public event EventHandler BackupAppsClicked;

        public EnterpriseAppsButtonPanel()
        {
            InitializePanel();

            // Create buttons
            btnSelectAll = CreateButton("✓ Select All");
            btnLoadBackup = CreateButton("📂 Load Backup");
            btnRestoreApps = CreateButton("♻️ Restore");
            btnBackupApps = CreateButton("💾 Backup");

            // Wire up events
            btnSelectAll.Click += (s, e) => SelectAllClicked?.Invoke(s, e);
            btnLoadBackup.Click += (s, e) => LoadBackupClicked?.Invoke(s, e);
            btnRestoreApps.Click += (s, e) => RestoreAppsClicked?.Invoke(s, e);
            btnBackupApps.Click += (s, e) => BackupAppsClicked?.Invoke(s, e);

            // Add controls to panel in a single row
            Controls.Add(btnSelectAll, 0, 0);
            Controls.Add(btnLoadBackup, 1, 0);
            Controls.Add(btnRestoreApps, 2, 0);
            Controls.Add(btnBackupApps, 3, 0);
        }

        private void InitializePanel()
        {
            Dock = DockStyle.Top;
            Height = 40;
            BackColor = Color.FromArgb(30, 30, 30);
            Padding = new Padding(10, 5, 10, 5);
            Margin = new Padding(0, 0, 0, 5);

            // Configure grid layout
            ColumnCount = 4;
            RowCount = 1;
            ColumnStyles.Clear();
            RowStyles.Clear();

            // Equal width columns
            for (int i = 0; i < 4; i++)
            {
                ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }
            RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        }

        private ModernButton CreateButton(string text)
        {
            return new ModernButton
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(5, 0, 5, 0),
                Height = 30,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = 
                {
                    BorderColor = Color.FromArgb(40, 40, 40),
                    BorderSize = 1,
                    MouseOverBackColor = Color.FromArgb(60, 60, 63),
                    MouseDownBackColor = Color.FromArgb(50, 50, 53)
                }
            };
        }

        public void EnableButtons(bool enable)
        {
            btnSelectAll.Enabled = enable;
            btnLoadBackup.Enabled = enable;
            btnRestoreApps.Enabled = enable;
            btnBackupApps.Enabled = enable;
        }
    }
}
