using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.IO;
using System.Windows.Documents;
using System.Globalization;

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
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"C:\\Program Files\\GrieferNET\\AutoTrackR2_Setup\\KillTrackR_MainScript.ps1\"",
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
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
                                    AdjustFontSize(PilotNameTextBox);
                                }
                                else if (e.Data.Contains("PlayerShip="))
                                {
                                    string playerShip = e.Data.Split('=')[1].Trim();
                                    PlayerShipTextBox.Text = playerShip;
                                    AdjustFontSize(PlayerShipTextBox);
                                }
                                else if (e.Data.Contains("GameMode="))
                                {
                                    string gameMode = e.Data.Split('=')[1].Trim();
                                    GameModeTextBox.Text = gameMode;
                                    AdjustFontSize(GameModeTextBox);
                                }
                                else if (e.Data.Contains("NewKill="))
                                {
                                    // Parse the kill data
                                    var killData = e.Data.Split('=')[1].Trim(); // Assume the kill data follows after "NewKill="
                                    var killParts = killData.Split(',');

                                    // Fetch the dynamic resource for AltTextColor
                                    var altTextColorBrush = new SolidColorBrush((Color)Application.Current.Resources["AltTextColor"]);

                                    // Fetch the Orbitron FontFamily from resources
                                    var orbitronFontFamily = (FontFamily)Application.Current.Resources["Orbitron"];
                                    var gemunuFontFamily = (FontFamily)Application.Current.Resources["Gemunu"];

                                    // Create a new TextBlock for each kill
                                    var killTextBlock = new TextBlock
                                    {
                                        Margin = new Thickness(0, 10, 0, 10),
                                        Style = (Style)Application.Current.Resources["RoundedTextBlock"], // Apply style for text
                                        FontSize = 14,
                                        FontWeight = FontWeights.Bold,
                                        FontFamily = gemunuFontFamily,
                                    };

                                    // Add styled content using Run elements
                                    killTextBlock.Inlines.Add(new Run("Victim Name: ")
                                    {
                                        Foreground = altTextColorBrush,
                                        FontFamily = orbitronFontFamily,
                                    });
                                    killTextBlock.Inlines.Add(new Run($"{killParts[1]}\n"));

                                    // Repeat for other lines
                                    killTextBlock.Inlines.Add(new Run("Victim Ship: ")
                                    {
                                        Foreground = altTextColorBrush,
                                        FontFamily = orbitronFontFamily,
                                    });
                                    killTextBlock.Inlines.Add(new Run($"{killParts[2]}\n"));

                                    killTextBlock.Inlines.Add(new Run("Victim Org: ")
                                    {
                                        Foreground = altTextColorBrush,
                                        FontFamily = orbitronFontFamily,
                                    });
                                    killTextBlock.Inlines.Add(new Run($"{killParts[3]}\n"));

                                    killTextBlock.Inlines.Add(new Run("Join Date: ")
                                    {
                                        Foreground = altTextColorBrush,
                                        FontFamily = orbitronFontFamily,
                                    });
                                    killTextBlock.Inlines.Add(new Run($"{killParts[4]}\n"));

                                    killTextBlock.Inlines.Add(new Run("UEE Record: ")
                                    {
                                        Foreground = altTextColorBrush,
                                        FontFamily = orbitronFontFamily,
                                    });
                                    killTextBlock.Inlines.Add(new Run($"{killParts[5]}\n"));

                                    killTextBlock.Inlines.Add(new Run("Kill Time: ")
                                    {
                                        Foreground = altTextColorBrush,
                                        FontFamily = orbitronFontFamily,
                                    });
                                    killTextBlock.Inlines.Add(new Run($"{killParts[6]}"));

                                    // Create a Border and apply the RoundedTextBlockWithBorder style
                                    var killBorder = new Border
                                    {
                                        Style = (Style)Application.Current.Resources["RoundedTextBlockWithBorder"], // Apply border style
                                        Child = killTextBlock // Set the TextBlock inside the Border
                                    };

                                    // Add the new Border to the StackPanel inside the Border
                                    KillFeedStackPanel.Children.Insert(0, killBorder);
                                }
                                else
                                {
                                    DebugPanel.AppendText(e.Data + Environment.NewLine);
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
                                DebugPanel.AppendText(e.Data + Environment.NewLine);
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

        private void AdjustFontSize(TextBlock textBlock)
        {
            // Set a starting font size
            double fontSize = 14;
            double maxWidth = textBlock.Width;

            if (string.IsNullOrEmpty(textBlock.Text) || double.IsNaN(maxWidth))
                return;

            // Measure the rendered width of the text
            FormattedText formattedText = new FormattedText(
                textBlock.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                fontSize,
                textBlock.Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            );

            // Reduce font size until text fits within the width
            while (formattedText.Width > maxWidth && fontSize > 6)
            {
                fontSize -= 0.5;
                formattedText = new FormattedText(
                    textBlock.Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                    fontSize,
                    textBlock.Foreground,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip
                );
            }

            // Apply the adjusted font size
            textBlock.FontSize = fontSize;
        }
    }
}
