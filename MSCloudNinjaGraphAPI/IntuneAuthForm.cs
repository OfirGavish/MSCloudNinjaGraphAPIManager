using System;
using System.Windows.Forms;
using Microsoft.Identity.Client;
using Microsoft.Graph;
using Azure.Identity;
using Microsoft.Kiota.Authentication.Azure;
using System.Drawing;
using System.Linq;
using Azure.Core;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Kiota.Abstractions;
using System.Net.Http;
using System.Net.Http.Headers;

namespace MSCloudNinjaGraphAPI
{
    public partial class IntuneAuthForm : Form
    {
        private readonly string[] _intuneScopes = new[]
        {
            "DeviceManagementConfiguration.Read.All",
            "DeviceManagementConfiguration.ReadWrite.All",
            "DeviceManagementApps.Read.All",
            "DeviceManagementApps.ReadWrite.All",
            "DeviceManagementManagedDevices.Read.All",
            "DeviceManagementManagedDevices.ReadWrite.All",
            "DeviceManagementServiceConfig.Read.All",
            "DeviceManagementServiceConfig.ReadWrite.All"
        };

        private Panel authPanel;
        private Button browserAuthButton;
        private Label statusLabel;
        public GraphServiceClient GraphClient { get; private set; }
        public string AccessToken { get; private set; }

        // Graph PowerShell Client ID
        private const string GraphPowerShellClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";

        public IntuneAuthForm()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "Intune Authentication";
            this.Size = new Size(400, 250);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            authPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 32, 32),
                Padding = new Padding(20)
            };

            browserAuthButton = new Button
            {
                Text = "Sign in to Intune",
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(200, 40),
                Location = new Point(90, 70)
            };
            browserAuthButton.Click += BrowserAuthButton_Click;

            statusLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Location = new Point(90, 130)
            };

            authPanel.Controls.Add(browserAuthButton);
            authPanel.Controls.Add(statusLabel);
            this.Controls.Add(authPanel);
        }

        private async void BrowserAuthButton_Click(object sender, EventArgs e)
        {
            try
            {
                statusLabel.Text = "Authenticating with Intune...";
                
                var intuneOptions = new InteractiveBrowserCredentialOptions
                {
                    TenantId = "organizations",
                    ClientId = GraphPowerShellClientId,
                    RedirectUri = new Uri("http://localhost"),
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
                };

                var intuneCredential = new InteractiveBrowserCredential(intuneOptions);
                var scopes = _intuneScopes.Select(s => $"https://graph.microsoft.com/{s}").ToArray();
                var token = await intuneCredential.GetTokenAsync(new TokenRequestContext(scopes));
                
                if (string.IsNullOrEmpty(token.Token))
                {
                    throw new Exception("Failed to obtain access token");
                }

                AccessToken = token.Token;
                System.Diagnostics.Debug.WriteLine($"Intune Token received: {AccessToken.Substring(0, 50)}...");

                // Create a request adapter with our custom auth provider
                var authProvider = new CustomAuthenticationProvider(AccessToken);
                var requestAdapter = new HttpClientRequestAdapter(authProvider);
                GraphClient = new GraphServiceClient(requestAdapter);

                // Test the connection with debug logging
                try
                {
                    // Test using direct HTTP client first
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
                        var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/deviceManagement/deviceConfigurations");
                        System.Diagnostics.Debug.WriteLine($"Direct HTTP test status: {response.StatusCode}");
                        
                        if (!response.IsSuccessStatusCode)
                        {
                            var error = await response.Content.ReadAsStringAsync();
                            System.Diagnostics.Debug.WriteLine($"Direct HTTP test error: {error}");
                            throw new Exception($"Test request failed with status {response.StatusCode}");
                        }
                    }

                    // Now test using Graph client
                    var testResult = await GraphClient.DeviceManagement.DeviceConfigurations.GetAsync();
                    System.Diagnostics.Debug.WriteLine("Graph client test request successful");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Test request failed: {ex.Message}");
                    throw;
                }
                
                statusLabel.Text = "Intune authentication successful!";
                statusLabel.ForeColor = Color.LightGreen;
                
                DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Authentication failed: " + ex.Message;
                statusLabel.ForeColor = Color.Red;
                GraphClient = null;
                AccessToken = null;
            }
        }
    }

    internal class CustomAuthenticationProvider : IAuthenticationProvider
    {
        private readonly string _accessToken;

        public CustomAuthenticationProvider(string accessToken)
        {
            _accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
            System.Diagnostics.Debug.WriteLine($"CustomAuthenticationProvider created with token: {_accessToken.Substring(0, 50)}...");
        }

        public Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine($"Adding token to request: {_accessToken.Substring(0, 50)}...");
            request.Headers.Add("Authorization", $"Bearer {_accessToken}");
            return Task.CompletedTask;
        }

        public AllowedHostsValidator AllowedHostsValidator => new AllowedHostsValidator();
    }
}
