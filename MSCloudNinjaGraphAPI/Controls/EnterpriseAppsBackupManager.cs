using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Graph.Models;
using MSCloudNinjaGraphAPI.Services;
using GraphApplication = Microsoft.Graph.Models.Application;

namespace MSCloudNinjaGraphAPI.Controls
{
    public class EnterpriseAppsBackupManager
    {
        private readonly IEnterpriseAppsService _service;
        private readonly EnterpriseAppsDialogManager _dialogManager;
        private List<ApplicationBackup> _backupApps;

        public event EventHandler<string> StatusUpdated;
        public event EventHandler<(string message, bool isError)> ErrorOccurred;
        public event EventHandler<List<GraphApplication>> BackupLoaded;

        public EnterpriseAppsBackupManager(IEnterpriseAppsService service, EnterpriseAppsDialogManager dialogManager)
        {
            _service = service;
            _dialogManager = dialogManager;
        }

        public async Task LoadBackupAsync()
        {
            var fileName = _dialogManager.ShowOpenDialog();
            if (string.IsNullOrEmpty(fileName)) return;

            try
            {
                _backupApps = await _service.LoadBackupAsync(fileName);
                StatusUpdated?.Invoke(this, $"Loaded {_backupApps.Count} applications from backup");
                BackupLoaded?.Invoke(this, _backupApps.Select(b => b.Application).ToList());
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ($"Error loading backup: {ex.Message}", true));
            }
        }

        public async Task SaveBackupAsync(IEnumerable<Microsoft.Graph.Models.Application> selectedApps)
        {
            if (selectedApps == null) return;

            var fileName = _dialogManager.ShowSaveDialog();
            if (string.IsNullOrEmpty(fileName)) return;

            try
            {
                await _service.SaveBackupAsync(selectedApps.ToList(), fileName);
                StatusUpdated?.Invoke(this, "Backup saved successfully");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ($"Error saving backup: {ex.Message}", true));
            }
        }

        public async Task RestoreAppsAsync()
        {
            if (_backupApps == null || _backupApps.Count == 0)
            {
                ErrorOccurred?.Invoke(this, ("No backup loaded to restore from", true));
                return;
            }

            try
            {
                int restoredCount = 0;
                foreach (var backupApp in _backupApps)
                {
                    await _service.RestoreApplicationAsync(backupApp);
                    restoredCount++;
                    StatusUpdated?.Invoke(this, $"Restored {restoredCount} of {_backupApps.Count} applications");
                }

                StatusUpdated?.Invoke(this, $"Successfully restored {restoredCount} applications");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ($"Error restoring applications: {ex.Message}", true));
            }
        }

        public bool HasBackup => _backupApps != null && _backupApps.Count > 0;
    }
}
