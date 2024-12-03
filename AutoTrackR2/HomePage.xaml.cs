using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.IO;

namespace AutoTrackR2
{
    public partial class HomePage : UserControl
    {
        public HomePage()
        {
            InitializeComponent();
        }

        private Process runningProcess; // Field to store the running process

        // Update Start/Stop button states based on the isRunning flag
        public void UpdateButtonState(bool isRunning)
        {
            var accentColor = (Color)Application.Current.Resources["AccentColor"];

            if (isRunning)
            {
                // Set Start button to "Running..." and apply glow effect
                StartButton.Content = "Running...";
                StartButton.IsEnabled = false; // Disable Start button
                StartButton.Style = (Style)FindResource("DisabledButtonStyle");

                // Add glow effect to the Start button
                StartButton.Effect = new DropShadowEffect
                {
                    Color = accentColor,
                    BlurRadius = 30,       // Adjust blur radius for desired glow intensity
                    ShadowDepth = 0,       // Set shadow depth to 0 for a pure glow effect
                    Opacity = 1,           // Set opacity for glow visibility
                    Direction = 0          // Direction doesn't matter for glow
                };

                StopButton.Style = (Style)FindResource("ButtonStyle");
                StopButton.IsEnabled = true;  // Enable Stop button
            }
            else
            {
                // Reset Start button back to its original state
                StartButton.Content = "Start";
                StartButton.IsEnabled = true;  // Enable Start button

                // Remove the glow effect from Start button
                StartButton.Effect = null;

                StopButton.Style = (Style)FindResource("DisabledButtonStyle");
                StartButton.Style = (Style)FindResource("ButtonStyle");
                StopButton.IsEnabled = false; // Disable Stop button
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KillTrackR_MainScript.ps1");
            TailFileAsync(scriptPath);
        }

        private async void TailFileAsync(string scriptPath)
        {
            await Task.Run(() =>
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    runningProcess = new Process { StartInfo = psi }; // Store the process in the field

                    runningProcess.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                // Parse and display key-value pairs in the OutputTextBox
                                if (e.Data.Contains("PlayerName="))
                                {
                                    string pilotName = e.Data.Split('=')[1].Trim();
                                    PilotNameTextBox.Text = pilotName; // Update the Button's Content
                                }
                                else if (e.Data.Contains("PlayerShip="))
                                {
                                    string playerShip = e.Data.Split('=')[1].Trim();
                                    PlayerShipTextBox.Text = playerShip;
                                }
                                else if (e.Data.Contains("GameMode="))
                                {
                                    string gameMode = e.Data.Split('=')[1].Trim();
                                    GameModeTextBox.Text = gameMode;
                                }
                                else if (e.Data.Contains("NewKill="))
                                {
                                    // Parse the kill data
                                    var killData = e.Data.Split('=')[1].Trim(); // Assume the kill data follows after "NewKill="
                                    var killParts = killData.Split(',');

                                    // Create a new TextBlock for each kill
                                    var killTextBlock = new TextBlock
                                    {
                                        Text = $"\nVictim Name: {killParts[1]}\nVictim Ship: {killParts[2]}\nVictim Org: {killParts[3]}\nJoin Date: {killParts[4]}\nUEE Record: {killParts[5]}\nKill Time: {killParts[6]}",
                                        Style = (Style)Application.Current.Resources["RoundedTextBox"],  // Apply the style from resources
                                        FontSize = 14,
                                        Margin = new Thickness(0, 10, 0, 10)
                                    };

                                    // Add the new TextBlock to the StackPanel inside the Border
                                    KillFeedStackPanel.Children.Add(killTextBlock);
                                }
                                else
                                {
                                    GameModeTextBox.Text = "ERROR";
                                }
                            });
                        }
                    };

                    runningProcess.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                            });
                        }
                    };

                    runningProcess.Start();
                    runningProcess.BeginOutputReadLine();
                    runningProcess.BeginErrorReadLine();

                    runningProcess.WaitForExit();
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Error running script: {ex.Message}");
                    });
                }
            });
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (runningProcess != null && !runningProcess.HasExited)
            {
                // Kill the running process
                runningProcess.Kill();
                runningProcess = null; // Clear the reference to the process
            }

            // Clear the text boxes
            System.Threading.Thread.Sleep(200);
            PilotNameTextBox.Text = string.Empty;
            PlayerShipTextBox.Text = string.Empty;
            GameModeTextBox.Text = string.Empty;
        }
    }
}
