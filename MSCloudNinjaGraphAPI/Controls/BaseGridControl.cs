using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace MSCloudNinjaGraphAPI.Controls
{
    public abstract class BaseGridControl<T> : UserControl where T : class
    {
        protected bool _isLoading;
        protected Label counterLabel;
        protected Label statusLabel;
        protected DataGridView gridView;
        protected ProgressBar loadingProgressBar;
        protected Panel topPanel;

        protected BaseGridControl(string backupType)
        {
            InitializeBaseComponents();
        }

        private void InitializeBaseComponents()
        {
            // Initialize loading progress bar
            loadingProgressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Height = 2,
                Dock = DockStyle.Top,
                Visible = false
            };

            statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                Text = ""
            };

            counterLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                Text = ""
            };

            topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40
            };

            Controls.Add(loadingProgressBar);
            Controls.Add(statusLabel);
            Controls.Add(counterLabel);
            Controls.Add(topPanel);
        }

        protected void ShowLoading(string message = "Loading...")
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ShowLoading(message)));
                return;
            }

            _isLoading = true;
            loadingProgressBar.Visible = true;
            statusLabel.Text = message;
            Cursor = Cursors.WaitCursor;
        }

        protected void HideLoading()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(HideLoading));
                return;
            }

            _isLoading = false;
            loadingProgressBar.Visible = false;
            statusLabel.Text = "";
            Cursor = Cursors.Default;
        }

        protected virtual void UpdateStatus(string message, bool isError = false)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(message, isError)));
                return;
            }

            statusLabel.Text = message;
            statusLabel.ForeColor = isError ? System.Drawing.Color.Red : System.Drawing.Color.Black;
        }

        protected void UpdateCounter(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateCounter(message)));
                return;
            }

            counterLabel.Text = message;
        }

        protected abstract List<T> GetSelectedItems();
        protected abstract Task RestoreItemsAsync(List<T> items);
    }
}
