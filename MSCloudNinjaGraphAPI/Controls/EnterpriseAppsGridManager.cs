using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Text.Json;
using Microsoft.Graph.Models;
using System.Linq;
using MSCloudNinjaGraphAPI.Services;
using GraphApplication = Microsoft.Graph.Models.Application;

namespace MSCloudNinjaGraphAPI.Controls
{
    public class EnterpriseAppsGridManager
    {
        private readonly DataGridView _grid;
        private readonly IEnterpriseAppsService _service;

        public event EventHandler<int> RowCountChanged;

        public EnterpriseAppsGridManager(DataGridView grid, IEnterpriseAppsService service)
        {
            _grid = grid;
            _service = service;
            InitializeGrid();
        }

        private void InitializeGrid()
        {
            EnterpriseAppsGridConfiguration.ConfigureGrid(_grid);
            _grid.CellContentClick += Grid_CellContentClick;
            _grid.CellValueChanged += Grid_CellValueChanged;
        }

        public void AddApplication(GraphApplication app)
        {
            int rowIdx = _grid.Rows.Add();
            var row = _grid.Rows[rowIdx];
            
            row.Cells["Select"].Value = false;
            row.Cells["DisplayName"].Value = app.DisplayName;
            row.Cells["AppId"].Value = app.AppId;
            row.Cells["PublisherDomain"].Value = app.PublisherDomain;
            row.Cells["SignInAudience"].Value = app.SignInAudience;
            row.Cells["Description"].Value = app.Description;
            row.Cells["Notes"].Value = app.Notes;
            row.Cells["RequiredResourceAccess"].Value = _service.FormatResourceAccess(app.RequiredResourceAccess);
            row.Cells["Api"].Value = _service.FormatApiSettings(app.Api);
            row.Cells["AppRoles"].Value = _service.FormatAppRoles(app.AppRoles);
            row.Cells["Info"].Value = _service.FormatInfo(app.Info);

            RowCountChanged?.Invoke(this, _grid.Rows.Count);
        }

        public void ClearGrid()
        {
            _grid.Rows.Clear();
            RowCountChanged?.Invoke(this, 0);
        }

        public void LoadApplications(IEnumerable<GraphApplication> apps)
        {
            ClearGrid();
            foreach (var app in apps.OrderBy(a => a.DisplayName))
            {
                AddApplication(app);
            }
        }

        public List<GraphApplication> GetSelectedApplications()
        {
            var selectedApps = new List<GraphApplication>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells["Select"].Value is bool isSelected && isSelected)
                {
                    selectedApps.Add(CreateApplicationFromRow(row));
                }
            }
            return selectedApps;
        }

        private GraphApplication CreateApplicationFromRow(DataGridViewRow row)
        {
            return new GraphApplication
            {
                DisplayName = row.Cells["DisplayName"].Value?.ToString(),
                AppId = row.Cells["AppId"].Value?.ToString(),
                PublisherDomain = row.Cells["PublisherDomain"].Value?.ToString(),
                SignInAudience = row.Cells["SignInAudience"].Value?.ToString(),
                Description = row.Cells["Description"].Value?.ToString(),
                Notes = row.Cells["Notes"].Value?.ToString(),
                RequiredResourceAccess = _service.ParseResourceAccess(row.Cells["RequiredResourceAccess"].Value?.ToString())?.ToList(),
                Api = _service.ParseApiSettings(row.Cells["Api"].Value?.ToString()),
                AppRoles = _service.ParseAppRoles(row.Cells["AppRoles"].Value?.ToString())?.ToList(),
                Info = _service.ParseInfo(row.Cells["Info"].Value?.ToString())
            };
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _grid.Columns["Select"].Index)
            {
                var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex] as DataGridViewCheckBoxCell;
                if (cell != null)
                {
                    cell.Value = !(cell.Value as bool? ?? false);
                    _grid.EndEdit();
                }
            }
        }

        private void Grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _grid.Columns["Select"].Index)
            {
                _grid.InvalidateCell(e.ColumnIndex, e.RowIndex);
            }
        }

        public void FilterApplications(string searchText, IEnumerable<GraphApplication> allApps)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                LoadApplications(allApps);
                return;
            }

            var filteredApps = allApps.Where(app =>
                (app.DisplayName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (app.AppId?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (app.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false));

            LoadApplications(filteredApps);
        }
    }
}
