using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AutoTrackR2
{
    public partial class UpdatePage : UserControl
    {
        private string currentVersion = "v2.0-beta.1";
        private string latestVersion;

        public UpdatePage()
        {
            InitializeComponent();
            CurrentVersionText.Text = currentVersion;
            CheckForUpdates();
        }

        private async void CheckForUpdates()
        {
            try
            {
                // Fetch the latest release info from GitHub
                latestVersion = await GetLatestVersionFromGitHub();

                // Update the Available Version field
                AvailableVersionText.Text = latestVersion;

                // Enable the Install button if a new version is available
                if (IsNewVersionAvailable(currentVersion, latestVersion))
                {
                    InstallButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                AvailableVersionText.Text = "Error checking updates.";
                MessageBox.Show($"Failed to check for updates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<string> GetLatestVersionFromGitHub()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "AutoTrackR2");

            string repoOwner = "BubbaGumpShrump";
            string repoName = "AutoTrackR2";

            try
            {
                // Attempt to fetch the latest release
                var url = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";
                var response = await client.GetStringAsync(url);

                // Parse the JSON using System.Text.Json
                using var document = System.Text.Json.JsonDocument.Parse(response);
                var root = document.RootElement;
                var tagName = root.GetProperty("tag_name").GetString();

                return tagName;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Fallback to releases list if 'latest' not found
                var url = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases";
                var response = await client.GetStringAsync(url);

                using var document = System.Text.Json.JsonDocument.Parse(response);
                var root = document.RootElement;

                // Get the tag name of the first release
                if (root.GetArrayLength() > 0)
                {
                    var firstRelease = root[0];
                    return firstRelease.GetProperty("tag_name").GetString();
                }

                throw new Exception("No releases found.");
            }
        }

        private bool IsNewVersionAvailable(string currentVersion, string latestVersion)
        {
            // Compare version strings (you can implement more complex version parsing logic if needed)
            return string.Compare(currentVersion, latestVersion, StringComparison.Ordinal) < 0;
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InstallButton.IsEnabled = false;
                InstallButton.Content = "Installing...";

                // Define the download URL
                string downloadUrl = "https://github.com/BubbaGumpShrump/AutoTrackR2/releases/download/v2.0-beta.1/AutoTrackR2_Setup.msi";

                // Download the installer before closing the app
                string installerPath = await DownloadInstaller(downloadUrl);

                // Launch the installer after the download completes
                RunInstaller(installerPath);

                // Gracefully close the app after starting the installer
                Application.Current.Shutdown();

                MessageBox.Show("Update installed successfully. Please restart the application.", "Update Installed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to install update: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                InstallButton.IsEnabled = true;
                InstallButton.Content = "Install Update";
            }
        }

        private async Task<string> DownloadInstaller(string url)
        {
            string tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutoTrackR2_Setup.msi");

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var fs = new System.IO.FileStream(tempFilePath, System.IO.FileMode.Create);
            await response.Content.CopyToAsync(fs);

            return tempFilePath;
        }

        private void RunInstaller(string installerPath)
        {
            try
            {
                // Prepare the command to run the .msi installer using msiexec
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "msiexec",
                    Arguments = $"/i \"{installerPath}\" /quiet /norestart",
                    UseShellExecute = false, // Ensures that the process runs in the background
                    CreateNoWindow = true    // Hides the command prompt window
                };

                // Start the process (this will run the installer)
                System.Diagnostics.Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start the installer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
