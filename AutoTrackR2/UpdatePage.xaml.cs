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
        private string currentVersion = "v2.0-beta.3";
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
                    InstallButton.Style = (Style)FindResource("ButtonStyle");
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
                InstallButton.Content = "Preparing to Install...";

                // Get the download URL for the latest release
                string downloadUrl = await GetLatestMsiDownloadUrlFromGitHub();

                // Download the installer to the user's Downloads folder
                string installerPath = await DownloadInstallerToDownloads(downloadUrl);

                // Launch the installer for manual installation
                RunInstaller(installerPath);

                // Gracefully close the app after launching the installer
                Application.Current.Shutdown();

                MessageBox.Show("Update installer has been downloaded. Please finish the installation manually.", "Update Ready", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download and launch the installer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                InstallButton.IsEnabled = true;
                InstallButton.Content = "Install Update";
            }
        }

        private async Task<string> GetLatestMsiDownloadUrlFromGitHub()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "AutoTrackR2");

            string repoOwner = "BubbaGumpShrump";
            string repoName = "AutoTrackR2";

            try
            {
                // Fetch the latest release info from GitHub
                var url = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";
                var response = await client.GetStringAsync(url);

                // Parse the JSON response
                using var document = System.Text.Json.JsonDocument.Parse(response);
                var root = document.RootElement;

                // Find the .msi asset in the release
                foreach (var asset in root.GetProperty("assets").EnumerateArray())
                {
                    string assetName = asset.GetProperty("name").GetString();
                    if (assetName.EndsWith(".msi"))
                    {
                        return asset.GetProperty("browser_download_url").GetString();
                    }
                }

                throw new Exception("No .msi file found in the latest release.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching the release data: {ex.Message}");
            }
        }

        private async Task<string> DownloadInstallerToDownloads(string url)
        {
            // Get the path to the user's Downloads folder (this works for OneDrive and other cloud storage setups)
            string downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string installerPath = Path.Combine(downloadsFolder, "AutoTrackR2_Setup.msi");

            // Ensure the downloads folder exists
            if (!Directory.Exists(downloadsFolder))
            {
                throw new Exception($"Downloads folder not found at: {downloadsFolder}");
            }

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // Write the downloaded content to the Downloads folder
            using var fs = new FileStream(installerPath, FileMode.Create);
            await response.Content.CopyToAsync(fs);

            return installerPath;
        }

        private void RunInstaller(string installerPath)
        {
            try
            {
                // Prepare the command to run the .msi installer using msiexec
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "msiexec",
                    Arguments = $"/i \"{installerPath}\" /norestart REINSTALLMODE=amus", // Silent install with no restart
                    UseShellExecute = true, // Ensures that the process runs in the background
                    CreateNoWindow = true    // Hides the command prompt window
                };

                // Start the process (this will run the installer)
                System.Diagnostics.Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open the installer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
