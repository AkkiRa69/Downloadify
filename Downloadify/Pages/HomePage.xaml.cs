using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Downloadify.Pages
{
    /// <summary>
    /// Interaction logic for HomePage.xaml
    /// </summary>
    public partial class HomePage : UserControl
    {
        private StreamManifest _streamManifest;
        private YoutubeClient _youtube;
        private string _selectedSavePath;
        private Dictionary<TextBox, string> placeholders = new Dictionary<TextBox, string>();
        public HomePage()
        {
            InitializeComponent();
            _youtube = new YoutubeClient();
            this.Loaded += HomePage_Loaded;
        }
        // Helper function to sanitize video titles for file names
        private string SanitizeFileName(string fileName)
        {
            // Define a list of invalid characters in file names and replace them with an empty string
            return string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            TextBlockPercent.Visibility = Visibility.Hidden;
            ProgressBarDownload.Visibility = Visibility.Hidden;
            placeholders[TextBoxUrl] = "https://";
        }
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (placeholders.ContainsKey(textBox) && textBox.Text == placeholders[textBox])
            {
                textBox.Text = "";
                textBox.Foreground = Brushes.Black;
            }
        }

        private async void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (placeholders.ContainsKey(textBox) && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = placeholders[textBox];
                textBox.Foreground = Brushes.Gray;
            }
            else
            {
                // Fetch video details automatically when the user loses focus
                await FetchVideoDetails();
            }
        }

        private async void ButtonDownload_Click(object sender, RoutedEventArgs e)
        {
            // Check if the user entered a valid YouTube URL
            string url = TextBoxUrl.Text;
            if (string.IsNullOrWhiteSpace(url))
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Please enter a valid YouTube URL."));
                return;
            }

            // Check if the user has selected a save path
            if (string.IsNullOrWhiteSpace(_selectedSavePath))
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Please choose a path to save the file."));
                return;
            }

            // Check if the file already exists
            if (File.Exists(_selectedSavePath))
            {
                // Ask the user if they want to replace the existing file
                var result = MessageBox.Show("A file with this name already exists. Do you want to replace it?",
                                             "File Exists", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Delete the existing file
                        File.Delete(_selectedSavePath);
                    }
                    catch (Exception ex)
                    {
                        ShowWindowMessage(new Components.MessageBoxDanger($"Failed to delete the old file: {ex.Message}"));
                        return; // Exit if deletion fails
                    }
                }
                else
                {
                    // If the user chooses not to replace, exit the download process

                    return;
                }
            }

            // Check if the user has selected a video quality
            if (ComboBoxQuality.SelectedIndex == -1)
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Please select a video quality."));
                return;
            }

            // Extract video ID from the URL
            string videoId = ExtractVideoId(url);
            if (videoId == null)
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Invalid YouTube URL."));
                return;
            }

            try
            {
                // If 'Audio Only' is checked
                if (CheckBoxAudioOnly.IsChecked == true)
                {
                    await DownloadAudioOnly(videoId);
                }
                else
                {
                    ProgressBarDownload.Visibility = Visibility.Visible;
                    TextBlockPercent.Visibility = Visibility.Visible;
                    await DownloadVideoAndAudio(videoId);
                }
            }
            catch (Exception ex)
            {
                ShowWindowMessage(new Components.MessageBoxDanger($"An error occurred: {ex.Message}"));
            }
        }

        private async Task FetchVideoDetails()
        {
            string url = TextBoxUrl.Text;
            string videoId = ExtractVideoId(url);
            if (videoId == null)
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Invalid YouTube URL."));
                return;
            }

            try
            {
                // Fetch video details
                var video = await _youtube.Videos.GetAsync(videoId);

                // Sanitize the video title
                string sanitizedTitle = SanitizeFileName(video.Title);

                // Set the sanitized title in the TextBox
                TextBoxTitle.Text = sanitizedTitle;

                // Fetch stream manifest and populate quality options
                _streamManifest = await _youtube.Videos.Streams.GetManifestAsync(videoId);

                var videoStreams = _streamManifest.GetVideoStreams().OrderByDescending(s => s.VideoQuality);

                // Convert Kbps to Mbps with correct formatting (2 decimal places)
                ComboBoxQuality.ItemsSource = videoStreams.Select(s => $"{s.VideoQuality.Label} - {(s.Bitrate.KiloBitsPerSecond / 8.0):F2} KB/s");

                // Enable download buttons once details are loaded
                ComboBoxQuality.IsEnabled = true;
                CheckBoxAudioOnly.IsEnabled = true;
                StackPanelGuide.Visibility = Visibility.Hidden;
                StackPanelVideoDetail.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                ShowWindowMessage(new Components.MessageBoxDanger($"Failed to load video details: {ex.Message}"));
            }
        }


        // Browse button for selecting the save location
        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                // Use the sanitized title for the default file name
                FileName = SanitizeFileName(TextBoxTitle.Text),
                Filter = "MP4 Files (*.mp4)|*.mp4|All Files (*.*)|*.*"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                _selectedSavePath = saveFileDialog.FileName;
                TextBoxSavePath.Text = _selectedSavePath; // Update UI to reflect selected path
                ButtonDownload.IsEnabled = true;
            }
        }


        // Download audio only
        private async Task DownloadAudioOnly(string videoId)
        {
            if (_streamManifest == null)
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Please fetch video details first."));
                return;
            }

            var audioStream = _streamManifest.GetAudioStreams().GetWithHighestBitrate();

            // Avoid overwriting the video+audio file by appending "_audio" to the filename
            string savePath = !string.IsNullOrEmpty(_selectedSavePath)
                ? Path.Combine(Path.GetDirectoryName(_selectedSavePath), Path.GetFileNameWithoutExtension(_selectedSavePath) + "_audio.mp3")
                : Path.Combine(Directory.GetCurrentDirectory(), "Videos", $"{TextBoxTitle.Text}_audio.mp3");

            await DownloadStream(audioStream, savePath);
        }

        // Helper function to download a stream and update the progress bar
        private async Task DownloadStream(IStreamInfo streamInfo, string savePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));

            await _youtube.Videos.Streams.DownloadAsync(streamInfo, savePath, new Progress<double>(progress =>
            {
                ProgressBarDownload.Value = progress * 100;
            }));

            ShowWindowMessage(new Components.MessageBoxSuccess());
        }

        // Extract video ID from URL
        private string ExtractVideoId(string url)
        {
            var regex = new Regex(@"(?:https?://)?(?:www\.)?(?:youtube\.com/watch\?v=|youtu\.be/)([a-zA-Z0-9_-]{11})");
            var match = regex.Match(url);
            return match.Success ? match.Groups[1].Value : null;
        }
        // Updated Download Video and Audio method to prevent freezing and sanitize file name
        private async Task DownloadVideoAndAudio(string videoId)
        {
            if (_streamManifest == null)
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Please fetch video details first."));
                return;
            }

            // Sanitize the title before using it as part of the file name
            string sanitizedTitle = SanitizeFileName(TextBoxTitle.Text);

            // Get the selected quality label from ComboBoxQuality
            string selectedQualityLabel = ComboBoxQuality.SelectedItem.ToString().Split(' ')[0]; // Extract the quality label part

            // Find the video stream that matches the selected quality label
            var videoStream = _streamManifest.GetVideoStreams()
                .FirstOrDefault(s => s.VideoQuality.Label == selectedQualityLabel);

            if (videoStream == null)
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Failed to find the selected video quality stream."));
                return;
            }

            var audioStream = _streamManifest.GetAudioStreams().GetWithHighestBitrate();

            // Use the user's selected save path for the final merged file
            string outputPath = _selectedSavePath;

            // Create temporary paths for the video and audio files
            string tempVideoPath = Path.Combine(Path.GetTempPath(), $"{sanitizedTitle}_video.mp4");
            string tempAudioPath = Path.Combine(Path.GetTempPath(), $"{sanitizedTitle}_audio.mp4");

            // Set up progress tracking
            double videoProgress = 0;
            double audioProgress = 0;
            double percentage = 0;

            // Download video and audio asynchronously without freezing UI
            var videoDownloadTask = _youtube.Videos.Streams.DownloadAsync(videoStream, tempVideoPath, new Progress<double>(progress =>
            {
                videoProgress = progress;
                percentage = (videoProgress + audioProgress) / 2 * 100;
                TextBlockPercent.Text = $"{percentage:F2}%";
                ProgressBarDownload.Value = percentage; // Combine video and audio progress
            })).AsTask();

            var audioDownloadTask = _youtube.Videos.Streams.DownloadAsync(audioStream, tempAudioPath, new Progress<double>(progress =>
            {
                audioProgress = progress;
                percentage = (videoProgress + audioProgress) / 2 * 100;
                TextBlockPercent.Text = $"{percentage:F2}%";
                ProgressBarDownload.Value = percentage; // Combine video and audio progress
            })).AsTask();

            // Wait for both downloads to complete asynchronously
            await Task.WhenAll(videoDownloadTask, audioDownloadTask);

            // Merge audio and video using FFmpeg asynchronously
            await Task.Run(() => MergeVideoAndAudio(tempVideoPath, tempAudioPath, outputPath));

            // Delete the temporary video and audio files after merging
            if (File.Exists(tempVideoPath)) File.Delete(tempVideoPath);
            if (File.Exists(tempAudioPath)) File.Delete(tempAudioPath);

            //MessageBox.Show($"Download completed! Saved to: {outputPath}");
            ShowWindowMessage(new Components.MessageBoxSuccess());

            if (App.isExplorerOpen)
            {
                OpenExplorerIfNotRunning(outputPath);
                App.isExplorerOpen = false;
            }
            App.main.AddPage(new HomePage());
        }

        public static void ShowWindowMessage(Window windowToShow, int topOffset = 0, int autoCloseDelay = 3000)
        {
            if (App.main != null)
            {
                // Set the position of the window at the top center of the parent window
                windowToShow.WindowStartupLocation = WindowStartupLocation.Manual; // Manual positioning
                windowToShow.Left = App.main.Left + (App.main.Width - windowToShow.Width) / 2; // Center horizontally
                windowToShow.Top = App.main.Top + topOffset; // Add top offset if needed

                windowToShow.Show();

                // Optionally, add a timer to close it after a specified delay
                CloseWindowAfterDelay(windowToShow, autoCloseDelay);
            }
        }

        private static async void CloseWindowAfterDelay(Window windowToClose, int delayInMilliseconds)
        {
            await Task.Delay(delayInMilliseconds);
            windowToClose.Close(); // Close the window after the delay
        }

        // Boolean flag to track if the folder is already open

        public void OpenExplorerIfNotRunning(string outputPath)
        {
            // Extract the directory path where the video is saved
            string directoryPath = Path.GetDirectoryName(outputPath);
            Process.Start("explorer.exe", $"/select,\"{outputPath}\"");
        }


        // Function to merge video and audio using FFmpeg
        private void MergeVideoAndAudio(string videoPath, string audioPath, string outputPath)
        {
            string ffmpegPath = Directory.GetCurrentDirectory() + "\\ffmpeg\\bin\\ffmpeg.exe"; // Make sure FFmpeg is installed and its path is provided
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac \"{outputPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit();
            }
        }

        private async void TextBoxUrl_TextChanged(object sender, TextChangedEventArgs e)
        {
            string url = TextBoxUrl.Text;

            // Optional: Ensure it's a valid YouTube URL format before fetching
            if (!string.IsNullOrWhiteSpace(url) && Regex.IsMatch(url, @"(?:https?://)?(?:www\.)?(?:youtube\.com/watch\?v=|youtu\.be/)([a-zA-Z0-9_-]{11})"))
            {
                StackPanelGuide.Visibility = Visibility.Hidden;
                ImageLoading.Visibility = Visibility.Visible;
                await FetchVideoDetails();
                ImageLoading.Visibility = Visibility.Hidden;
            }
        }

        private void TextBlockContactUs_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://t.me/monyakkhara",
                UseShellExecute = true // This is required to use the default browser
            });
        }
    }
}
