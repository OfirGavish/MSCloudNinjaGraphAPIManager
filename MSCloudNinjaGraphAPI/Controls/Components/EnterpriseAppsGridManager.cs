using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using MSCloudNinjaGraphAPI.Services;
using GraphApplication = Microsoft.Graph.Models.Application;

namespace MSCloudNinjaGraphAPI.Controls.Components
{
    public class EnterpriseAppsGridManager
    {
        public event EventHandler<int> RowCountChanged;

        private readonly DataGridView _grid;
        private readonly IEnterpriseAppsService _service;
        private List<GraphApplication> _currentApps;

        public EnterpriseAppsGridManager(DataGridView grid, IEnterpriseAppsService service)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _currentApps = new List<GraphApplication>();
        }

        public void LoadApplications(IEnumerable<GraphApplication> apps)
        {
            _currentApps = apps.ToList();
            RefreshGrid(_currentApps);
        }

        public void FilterApplications(string searchText, IEnumerable<GraphApplication> apps)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                RefreshGrid(apps);
                return;
            }

            var filteredApps = apps.Where(app =>
                (app.DisplayName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (app.AppId?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (app.Id?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
            );

            RefreshGrid(filteredApps);
        }

        public List<GraphApplication> GetSelectedApplications()
        {
            var selectedApps = new List<GraphApplication>();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells["Select"].Value is true)
                {
                    var appId = row.Cells["AppId"].Value?.ToString();
                    var app = _currentApps.FirstOrDefault(a => a.AppId == appId);
                    if (app != null)
                    {
                        selectedApps.Add(app);
                    }
                }
            }

            return selectedApps;
        }

        private void RefreshGrid(IEnumerable<GraphApplication> apps)
        {
            _grid.Rows.Clear();

            foreach (var app in apps)
            {
                var row = new object[]
                {
                    false, // Select checkbox
                    app.Id,
                    app.DisplayName,
                    app.AppId,
                    app.PublisherDomain,
                    app.SignInAudience,
                    app.Description,
                    app.Notes,
                    string.Join(", ", app.IdentifierUris ?? new List<string>()),
                    _service.FormatResourceAccess(app.RequiredResourceAccess),
                    _service.FormatApiSettings(app.Api),
                    _service.FormatAppRoles(app.AppRoles),
                    _service.FormatInfo(app.Info),
                    app.IsDeviceOnlyAuthSupported,
                    app.IsFallbackPublicClient,
                    string.Join(", ", app.Tags ?? new List<string>())
                };

                _grid.Rows.Add(row);
            }

            OnRowCountChanged(apps.Count());
        }

        private void OnRowCountChanged(int count)
        {
            RowCountChanged?.Invoke(this, count);
        }
    }
}
