using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Graph.Models;
using MSCloudNinjaGraphAPI.Services;
using System.ComponentModel;
using GraphApplication = Microsoft.Graph.Models.Application;

namespace MSCloudNinjaGraphAPI.Controls.Components
{
    public class EnterpriseAppsBackupManager
    {
        public event EventHandler<string> StatusUpdated;
        public event EventHandler<(string message, bool isError)> ErrorOccurred;
        public event EventHandler<List<GraphApplication>> BackupLoaded;

        private readonly IEnterpriseAppsService _service;
        private readonly EnterpriseAppsDialogManager _dialogManager;
        private List<ApplicationBackup> _loadedBackups;

        public EnterpriseAppsBackupManager(IEnterpriseAppsService service, EnterpriseAppsDialogManager dialogManager)
        {
            _service = service;
            _dialogManager = dialogManager;
            _loadedBackups = new List<ApplicationBackup>();
        }

        public async Task LoadBackupAsync()
        {
            try
            {
                var backupPath = _dialogManager.ShowOpenFileDialog();
                if (string.IsNullOrEmpty(backupPath))
                {
                    return;
                }

                OnStatusUpdated("Loading backup...");
                _loadedBackups = await _service.LoadBackupAsync(backupPath);
                OnStatusUpdated($"Loaded {_loadedBackups.Count} applications from backup");

                OnBackupLoaded(_loadedBackups.Select(b => b.Application).ToList());
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error loading backup: {ex.Message}", true);
            }
        }

        public async Task RestoreAppsAsync()
        {
            try
            {
                if (_loadedBackups == null || _loadedBackups.Count == 0)
                {
                    OnErrorOccurred("No backup loaded to restore from", true);
                    return;
                }

                OnStatusUpdated("Restoring applications...");
                foreach (var backup in _loadedBackups)
                {
                    await _service.RestoreApplicationAsync(backup);
                }
                OnStatusUpdated($"Restored {_loadedBackups.Count} applications");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error restoring applications: {ex.Message}", true);
            }
        }

        public async Task SaveBackupAsync(List<GraphApplication> apps)
        {
            try
            {
                if (apps == null || apps.Count == 0)
                {
                    OnErrorOccurred("No applications selected for backup", true);
                    return;
                }

                var savePath = _dialogManager.ShowSaveFileDialog();
                if (string.IsNullOrEmpty(savePath))
                {
                    return;
                }

                OnStatusUpdated("Creating backup...");
                await _service.SaveBackupAsync(apps, savePath);
                OnStatusUpdated($"Backed up {apps.Count} applications");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error creating backup: {ex.Message}", true);
            }
        }

        private void OnStatusUpdated(string message)
        {
            StatusUpdated?.Invoke(this, message);
        }

        private void OnErrorOccurred(string message, bool isError)
        {
            ErrorOccurred?.Invoke(this, (message, isError));
        }

        private void OnBackupLoaded(List<GraphApplication> apps)
        {
            BackupLoaded?.Invoke(this, apps);
        }
    }
}
