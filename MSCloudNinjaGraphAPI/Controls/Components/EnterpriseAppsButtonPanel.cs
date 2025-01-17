using System;
using System.Windows.Forms;
using System.Drawing;

namespace MSCloudNinjaGraphAPI.Controls.Components
{
    public class EnterpriseAppsButtonPanel : Panel
    {
        public event EventHandler SelectAllClicked;
        public event EventHandler LoadBackupClicked;
        public event EventHandler RestoreAppsClicked;
        public event EventHandler BackupAppsClicked;

        private readonly Button btnSelectAll;
        private readonly Button btnLoadBackup;
        private readonly Button btnRestoreApps;
        private readonly Button btnBackupApps;

        public EnterpriseAppsButtonPanel()
        {
            Dock = DockStyle.Top;
            Height = 200;
            Padding = new Padding(5);

            btnSelectAll = CreateButton("Select All", 0);
            btnLoadBackup = CreateButton("Load Backup", 1);
            btnRestoreApps = CreateButton("Restore Apps", 2);
            btnBackupApps = CreateButton("Backup Selected", 3);

            btnSelectAll.Click += (s, e) => SelectAllClicked?.Invoke(this, EventArgs.Empty);
            btnLoadBackup.Click += (s, e) => LoadBackupClicked?.Invoke(this, EventArgs.Empty);
            btnRestoreApps.Click += (s, e) => RestoreAppsClicked?.Invoke(this, EventArgs.Empty);
            btnBackupApps.Click += (s, e) => BackupAppsClicked?.Invoke(this, EventArgs.Empty);
        }

        private Button CreateButton(string text, int position)
        {
            var button = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 30,
                Margin = new Padding(0, 5, 0, 5),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            Controls.Add(button);
            return button;
        }
    }
}
