using Microsoft.Graph;
using MSCloudNinjaGraphAPI.Controls;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;
using System.IO;
using Azure.Identity;
using Microsoft.Kiota.Authentication.Azure;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using GraphApiClient;
using System.Runtime.InteropServices;
using Microsoft.Identity.Client;

// And Eric Cartman said: Respect My Authoritah!
namespace MSCloudNinjaGraphAPI
{
    public partial class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            if (IsWindows10OrGreater(17763))
            {
                var attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                if (IsWindows10OrGreater(18985))
                {
                    attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
                }

                int useImmersiveDarkMode = enabled ? 1 : 0;
                return DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
            }

            return false;
        }

        private static bool IsWindows10OrGreater(int build = -1)
        {
            return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= build;
        }

        private GraphServiceClient _graphClient = null!;
        private readonly string[] _scopes = new[] 
        { 
            "Policy.Read.All",
            "Policy.ReadWrite.ConditionalAccess",
            "Application.Read.All",
            "DeviceManagementConfiguration.Read.All",
            "DeviceManagementConfiguration.ReadWrite.All",
            "DeviceManagementApps.Read.All",
            "DeviceManagementApps.ReadWrite.All",
            "DeviceManagementManagedDevices.Read.All",
            "DeviceManagementManagedDevices.ReadWrite.All",
            "DeviceManagementServiceConfig.Read.All",
            "DeviceManagementServiceConfig.ReadWrite.All"
        };

        private Panel authPanel = null!;
        private Button browserAuthButton = null!;
        private Button appRegAuthButton = null!;
        private TextBox clientIdTextBox = null!;
        private TextBox tenantIdTextBox = null!;
        private TextBox clientSecretTextBox = null!;
        private Label statusLabel = null!;
        private ConditionalAccessControl conditionalAccessControl;
        private EnterpriseAppsControl enterpriseAppsControl;
        private IntuneControl intuneControl;
        private ModernButton btnConditionalAccess;
        private ModernButton btnEnterpriseApps;
        private ModernButton btnIntune;

        public MainForm()
        {
            InitializeComponent();
            UseImmersiveDarkMode(Handle, true); // Enable dark mode for title bar
            SetupForm();
            SetupAuthPanel();
        }

        private void SetupAuthPanel()
        {
            // Create auth panel with modern styling
            authPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeColors.ContentBackground,
                Padding = new Padding(30)
            };

            var authContainer = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = ThemeColors.GridBackground,
                Padding = new Padding(30),
                Location = new Point(0, 50)
            };

            var authLabel = new Label
            {
                Text = "Choose Authentication Method",
                Font = new Font("Segoe UI Light", 24),
                ForeColor = ThemeColors.TextLight,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 20)
            };

            browserAuthButton = new ModernButton
            {
                Text = "User Authentication",
                Width = 400,
                Margin = new Padding(0, 10, 0, 10)
            };
            browserAuthButton.Click += BrowserAuthButton_Click;

            appRegAuthButton = new ModernButton
            {
                Text = "App Registration Authentication",
                Width = 400,
                Margin = new Padding(0, 10, 0, 20)
            };
            appRegAuthButton.Click += AppRegAuthButton_Click;

            // Create input fields for app registration
            var clientIdLabel = new Label
            {
                Text = "Client ID",
                Font = new Font("Segoe UI", 10),
                ForeColor = ThemeColors.TextLight,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 5)
            };

            clientIdTextBox = new TextBox
            {
                Width = 400,
                Font = new Font("Segoe UI", 10),
                BackColor = ThemeColors.GridBackground,
                ForeColor = ThemeColors.TextLight,
                BorderStyle = BorderStyle.FixedSingle
            };

            var tenantIdLabel = new Label
            {
                Text = "Tenant ID",
                Font = new Font("Segoe UI", 10),
                ForeColor = ThemeColors.TextLight,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 5)
            };

            tenantIdTextBox = new TextBox
            {
                Width = 400,
                Font = new Font("Segoe UI", 10),
                BackColor = ThemeColors.GridBackground,
                ForeColor = ThemeColors.TextLight,
                BorderStyle = BorderStyle.FixedSingle
            };

            var clientSecretLabel = new Label
            {
                Text = "Client Secret",
                Font = new Font("Segoe UI", 10),
                ForeColor = ThemeColors.TextLight,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 5)
            };

            clientSecretTextBox = new TextBox
            {
                Width = 400,
                Font = new Font("Segoe UI", 10),
                BackColor = ThemeColors.GridBackground,
                ForeColor = ThemeColors.TextLight,
                BorderStyle = BorderStyle.FixedSingle,
                PasswordChar = '•'
            };

            statusLabel = new Label
            {
                Text = "Please choose an authentication method",
                Font = new Font("Segoe UI", 10),
                ForeColor = ThemeColors.TextDark,
                AutoSize = true,
                Margin = new Padding(0, 20, 0, 0)
            };

            // Create FlowLayoutPanel for vertical stacking
            var flowLayout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false
            };

            // Add controls to flow layout
            flowLayout.Controls.AddRange(new Control[] 
            { 
                authLabel,
                browserAuthButton,
                appRegAuthButton,
                clientIdLabel,
                clientIdTextBox,
                tenantIdLabel,
                tenantIdTextBox,
                clientSecretLabel,
                clientSecretTextBox,
                statusLabel
            });

            // Add flow layout to auth container
            authContainer.Controls.Add(flowLayout);
            
            // Center the auth container
            authContainer.Location = new Point(
                (authPanel.ClientSize.Width - authContainer.Width) / 2,
                (authPanel.ClientSize.Height - authContainer.Height) / 2);

            // Add auth container to auth panel
            authPanel.Controls.Add(authContainer);

            // Initially show only the auth panel
            mainContent.Controls.Clear();
            mainContent.Controls.Add(authPanel);
        }

        private async void BrowserAuthButton_Click(object sender, EventArgs e)
        {
            try
            {
                statusLabel.Text = "Opening browser for authentication...";
                System.Windows.Forms.Application.DoEvents();

                var options = new InteractiveBrowserCredentialOptions
                {
                    TenantId = "organizations"
                };

                var credential = new InteractiveBrowserCredential(options);
                var authProvider = new AzureIdentityAuthenticationProvider(credential);
                var requestAdapter = new HttpClientRequestAdapter(authProvider);
                _graphClient = new GraphServiceClient(requestAdapter);

                // Test authentication by making a simple API call
                var users = await _graphClient.Users.GetAsync();

                if (users?.Value?.FirstOrDefault() != null)
                {
                    statusLabel.Text = $"Authentication successful! Welcome {users.Value.First().DisplayName}";
                    InitializeMainInterface();
                }
                else
                {
                    throw new Exception("Could not verify user authentication");
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Authentication failed: {ex.Message}";
                MessageBox.Show($"Error during authentication: {ex.Message}", "Authentication Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void AppRegAuthButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(clientIdTextBox.Text) ||
                    string.IsNullOrWhiteSpace(tenantIdTextBox.Text) ||
                    string.IsNullOrWhiteSpace(clientSecretTextBox.Text))
                {
                    MessageBox.Show("Please fill in all the required fields.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                statusLabel.Text = "Initializing app registration authentication...";
                System.Windows.Forms.Application.DoEvents();

                var credentials = new ClientSecretCredential(
                    tenantIdTextBox.Text,
                    clientIdTextBox.Text,
                    clientSecretTextBox.Text);

                var authProvider = new AzureIdentityAuthenticationProvider(credentials);
                var requestAdapter = new HttpClientRequestAdapter(authProvider);
                _graphClient = new GraphServiceClient(requestAdapter);

                // Test authentication by making a simple API call
                await _graphClient.Users.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Top = 1;
                    requestConfiguration.QueryParameters.Select = new[] { "id" };
                });

                statusLabel.Text = "Authentication successful!";
                InitializeMainInterface();
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Authentication failed: {ex.Message}";
                MessageBox.Show($"Error during authentication: {ex.Message}", "Authentication Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeMainInterface()
        {
            try
            {
                // Hide auth panel and show main interface
                if (authPanel != null)
                {
                    mainContent.Controls.Remove(authPanel);
                    authPanel.Dispose();
                }

                // Make sure mainContent is visible
                mainContent.Visible = true;

                bool hasAnyAccess = false;
                bool hasConditionalAccess = false;
                bool hasEnterpriseApps = false;
                bool hasIntune = false;

                // Try initializing each control separately
                try
                {
                    conditionalAccessControl = new ConditionalAccessControl(_graphClient);
                    hasConditionalAccess = true;
                    hasAnyAccess = true;
                }
                catch (Exception) { }

                try 
                {
                    enterpriseAppsControl = new EnterpriseAppsControl(_graphClient);
                    hasEnterpriseApps = true;
                    hasAnyAccess = true;
                }
                catch (Exception) { }

                try
                {
                    intuneControl = new IntuneControl(_graphClient);
                    hasIntune = true;
                    hasAnyAccess = true;
                }
                catch (Exception) { }

                if (!hasAnyAccess)
                {
                    throw new Exception("Insufficient permissions to access any functionality");
                }

                // Show warning if some controls are not available
                if (!(hasConditionalAccess && hasEnterpriseApps && hasIntune))
                {
                    MessageBox.Show("Only the parts your user or application has access to will be available", 
                        "Limited Access", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                // Show first available control
                if (hasConditionalAccess)
                    SwitchToControl(conditionalAccessControl);
                else if (hasEnterpriseApps)
                    SwitchToControl(enterpriseAppsControl);
                else if (hasIntune)
                    SwitchToControl(intuneControl);

                // Force a redraw
                mainContent.Invalidate(true);
                this.Invalidate(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during authentication: {ex.Message}", "Authentication Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetupForm()
        {
            // Form settings
            this.Text = "MSCloudNinja GraphAPI Manager";
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                string projectRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(exePath), "..", "..", ".."));
                string logoPath = Path.Combine(projectRoot, "assets", "logo.png");
                
                if (System.IO.File.Exists(logoPath))
                {
                    using (var bitmap = new Bitmap(logoPath))
                    {
                        IntPtr hIcon = bitmap.GetHicon();
                        try
                        {
                            this.Icon = Icon.FromHandle(hIcon);
                        }
                        finally
                        {
                            NativeMethods.DestroyIcon(hIcon);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue without icon
                System.Diagnostics.Debug.WriteLine($"Failed to load icon: {ex.Message}");
            }
            
            this.Size = new Size(1600, 900);
            this.MinimumSize = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ThemeColors.BackgroundDark;

            // Setup header panel
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 50;
            headerPanel.BackColor = ThemeColors.HeaderBackground;
            headerPanel.Padding = new Padding(10, 5, 10, 5);

            // Add logo to header
            var logo = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                Height = 32,
                Width = 32,
                Location = new Point(10, 9)
            };

            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MSCloudNinjaGraphAPI.logo.png"))
                {
                    if (stream != null)
                    {
                        logo.Image = Image.FromStream(stream);
                    }
                }
            }
            catch (Exception)
            {
                // Handle logo loading error if needed
            }

            // Add title to header with modern font
            var title = new Label
            {
                Text = "MSCloudNinja GraphAPI Manager",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = ThemeColors.TextLight,
                AutoSize = true,
                Location = new Point(50, 14)
            };

            // Create navigation buttons
            btnConditionalAccess = new ModernButton
            {
                Text = "Conditional Access",
                Width = 150,
                Height = 30,
                BackColor = ThemeColors.HeaderBackground,
                ForeColor = ThemeColors.TextLight,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(400, 10)
            };
            btnConditionalAccess.Click += (s, e) => btnConditionalAccess_Click(s, e);

            btnEnterpriseApps = new ModernButton
            {
                Text = "Enterprise Apps",
                Width = 150,
                Height = 30,
                BackColor = ThemeColors.HeaderBackground,
                ForeColor = ThemeColors.TextLight,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(560, 10)
            };
            btnEnterpriseApps.Click += (s, e) => btnEnterpriseApps_Click(s, e);

             // Create navigation buttons
            btnIntune = new ModernButton
            {
                Text = "Intune",
                Width = 150,
                Height = 30,
                BackColor = ThemeColors.HeaderBackground,
                ForeColor = ThemeColors.TextLight,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(720, 10)
            };
            btnIntune.Click += (s, e) => btnIntuneManagement_Click(s, e);

            // Add controls to header
            headerPanel.Controls.AddRange(new Control[] { logo, title, btnConditionalAccess, btnEnterpriseApps, btnIntune });

            // Setup main content panel
            mainContent.Dock = DockStyle.Fill;
            mainContent.BackColor = ThemeColors.ContentBackground;
            mainContent.Padding = new Padding(10);

            // Create a container panel for proper layout
            var containerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeColors.ContentBackground
            };

            // Add mainContent to container
            containerPanel.Controls.Add(mainContent);

            // Add panels to form in correct order
            this.Controls.Add(containerPanel);
            this.Controls.Add(headerPanel);
            headerPanel.BringToFront();

            // Set padding for container to account for header
            containerPanel.Padding = new Padding(0, headerPanel.Height, 0, 0);
        }

        private class NativeMethods
        {
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern bool DestroyIcon(IntPtr handle);
        }

        private class TokenProvider : IAccessTokenProvider
        {
            private readonly string _token;

            public TokenProvider(string token)
            {
                _token = token;
            }

            public AllowedHostsValidator AllowedHostsValidator { get; } = new AllowedHostsValidator();

            public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object> additionalAuthenticationContext = default, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_token);
            }
        }

        private void btnConditionalAccess_Click(object sender, EventArgs e)
        {
            if (!IsAuthenticated())
            {
                MessageBox.Show("Please authenticate first.", "Authentication Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnConditionalAccess.FlatAppearance.BorderColor = Color.DodgerBlue;
            btnConditionalAccess.FlatAppearance.BorderSize = 2;
            btnEnterpriseApps.FlatAppearance.BorderColor = Color.FromArgb(40, 40, 40);
            btnEnterpriseApps.FlatAppearance.BorderSize = 1;

            SwitchToControl(conditionalAccessControl);
        }

        private void btnEnterpriseApps_Click(object sender, EventArgs e)
        {
            if (!IsAuthenticated())
            {
                MessageBox.Show("Please authenticate first.", "Authentication Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnEnterpriseApps.FlatAppearance.BorderColor = Color.DodgerBlue;
            btnEnterpriseApps.FlatAppearance.BorderSize = 2;
            btnConditionalAccess.FlatAppearance.BorderColor = Color.FromArgb(40, 40, 40);
            btnConditionalAccess.FlatAppearance.BorderSize = 1;

            SwitchToControl(enterpriseAppsControl);
        }

        private void btnIntuneManagement_Click(object sender, EventArgs e)
        {
            if (!IsAuthenticated())
            {
                MessageBox.Show("Please authenticate first.", "Authentication Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SwitchToControl(intuneControl);
        }

        private bool IsAuthenticated()
        {
            return _graphClient != null;
        }

        private void SwitchToControl(Control control)
        {
            // Update button states
            btnConditionalAccess.BackColor = control == conditionalAccessControl ? 
                Color.FromArgb(60, 60, 60) : ThemeColors.HeaderBackground;
            btnEnterpriseApps.BackColor = control == enterpriseAppsControl ? 
                Color.FromArgb(60, 60, 60) : ThemeColors.HeaderBackground;
            btnIntune.BackColor = control == intuneControl ? 
                Color.FromArgb(60, 60, 60) : ThemeColors.HeaderBackground;

            // Switch the visible control
            mainContent.Controls.Clear();
            control.Dock = DockStyle.Fill;
            mainContent.Controls.Add(control);
        }
    }
}
