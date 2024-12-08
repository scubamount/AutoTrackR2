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

                // Download and install the latest version
                string downloadUrl = await GetDownloadUrlFromGitHub();
                await DownloadAndInstallUpdate(downloadUrl);

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

        private async Task<string> GetDownloadUrlFromGitHub()
        {
            // Use the exact URL since we know it
            return "https://github.com/BubbaGumpShrump/AutoTrackR2/releases/download/v2.0-beta.1/AutoTrackR2_Setup.msi";
        }
        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
                {
                    return false; // File is not locked
                }
            }
            catch (IOException)
            {
                return true; // File is locked
            }
        }

        private async Task DownloadAndInstallUpdate(string url)
        {
            string tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutoTrackR2_Setup.msi");

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var fs = new System.IO.FileStream(tempFilePath, System.IO.FileMode.Create);
            await response.Content.CopyToAsync(fs);

            // Wait for the system to release the file
            await Task.Delay(2000); // Wait for 2 seconds

            // Check if the file is locked
            if (IsFileLocked(tempFilePath))
            {
                MessageBox.Show("The installer file is locked by another process.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Launch the installer
                System.Diagnostics.Process.Start(tempFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start the installer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
