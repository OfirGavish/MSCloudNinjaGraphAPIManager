using System;
using System.Windows.Forms;

namespace MSCloudNinjaGraphAPI.Controls
{
    public class EnterpriseAppsDialogManager
    {
        public SaveFileDialog SaveFileDialog { get; }
        public OpenFileDialog OpenFileDialog { get; }
        public FolderBrowserDialog FolderBrowserDialog { get; }

        public EnterpriseAppsDialogManager()
        {
            SaveFileDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                Title = "Save Backup"
            };

            OpenFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                Title = "Load Backup"
            };

            FolderBrowserDialog = new FolderBrowserDialog
            {
                Description = "Select folder for backup",
                UseDescriptionForTitle = true
            };
        }

        public string ShowSaveDialog()
        {
            return SaveFileDialog.ShowDialog() == DialogResult.OK ? SaveFileDialog.FileName : null;
        }

        public string ShowOpenDialog()
        {
            return OpenFileDialog.ShowDialog() == DialogResult.OK ? OpenFileDialog.FileName : null;
        }

        public string ShowFolderDialog()
        {
            return FolderBrowserDialog.ShowDialog() == DialogResult.OK ? FolderBrowserDialog.SelectedPath : null;
        }
    }
}
