using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace Downloadify.Pages
{
    /// <summary>
    /// Interaction logic for HomePage.xaml
    /// </summary>
    public partial class HomePage : UserControl
    {
        private StreamManifest _streamManifest;
        private YoutubeClient _youtube;

        private Dictionary<TextBox, string> placeholders = new Dictionary<TextBox, string>();
        public HomePage()
        {
            InitializeComponent();
            _youtube = new YoutubeClient();
            TextBoxTitle.TextChanged += TextBoxTitle_TextChanged;
            this.Loaded += HomePage_Loaded;
        }

        private async void ButtonDownload_Click(object sender, RoutedEventArgs e)
        {
            string url = TextBoxUrl.Text;
            if (string.IsNullOrWhiteSpace(url))
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Please enter a valid YouTube URL."));
                return;
            }
            if (string.IsNullOrWhiteSpace(App.selectedSavePath))
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Please choose a path to save the file."));
                return;
            }
            if (File.Exists(App.selectedSavePath))
            {
                var result = MessageBox.Show("A file with this name already exists. Do you want to replace it?",
                                             "File Exists", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Delete(App.selectedSavePath);
                    }
                    catch (Exception ex)
                    {
                        ShowWindowMessage(new Components.MessageBoxDanger($"Failed to delete the old file: {ex.Message}"));
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
            if (CheckBoxMp4.IsChecked == false && CheckBoxAudioOnly.IsChecked == false)
            {
                CheckBoxMp4.IsChecked = true;
            }

            if (ComboBoxQuality.SelectedIndex == -1)
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Please select a video quality."));
                return;
            }

            string videoId = ExtractVideoId(url);
            if (videoId == null)
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Invalid YouTube URL."));
                return;
            }

            try
            {
                ProgressBarDownload.Visibility = Visibility.Visible;
                TextBlockPercent.Visibility = Visibility.Visible;

                if (CheckBoxAudioOnly.IsChecked == true)
                {
                    await DownloadAudioOnly(videoId);
                }
                else if (CheckBoxMp4.IsChecked == true)
                {
                    await DownloadVideoAndAudio(videoId);
                }
            }
            catch (Exception ex)
            {
                ShowWindowMessage(new Components.MessageBoxDanger($"An error occurred: {ex.Message}"));
            }
        }

        public async Task ConvertToWavAsync(string inputFilePath)
        {
            await Task.Run(() => ConvertToWav(inputFilePath));
        }

        public void ConvertToWav(string inputFilePath)
        {
            string outputFilePath = Path.ChangeExtension(inputFilePath, ".wav");
            string ffmpegPath = Directory.GetCurrentDirectory() + "\\ffmpeg\\ffmpeg.exe"; // Path to FFmpeg executable
            string arguments = $"-i \"{inputFilePath}\" \"{outputFilePath}\"";

            Process ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            ffmpeg.Start();
            ffmpeg.WaitForExit();

            if (File.Exists(outputFilePath))
            {
                MessageBox.Show($"Conversion to WAV successful: {outputFilePath}");
            }
            else
            {
                MessageBox.Show("Error: Conversion to WAV failed.");
            }
        }

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                FileName = SanitizeFileName(TextBoxTitle.Text),
                Filter = "MP4 Files (*.mp4)|*.mp4"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                App.selectedSavePath = saveFileDialog.FileName;
                TextBoxSavePath.Text = App.selectedSavePath; // Update UI to reflect selected path
                ButtonDownload.IsEnabled = true;
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
                var video = await _youtube.Videos.GetAsync(videoId);

                string sanitizedTitle = SanitizeFileName(video.Title);
                TextBoxTitle.Text = sanitizedTitle;

                if (!string.IsNullOrEmpty(App.selectedSavePath))
                {
                    ButtonDownload.IsEnabled = true;
                }

                TextBoxSavePath.Text = App.selectedSavePath; // Ensure UI reflects the current save path
                _streamManifest = await _youtube.Videos.Streams.GetManifestAsync(videoId);
                StackPanelVideoDetail.Visibility = Visibility.Visible;
                if (CheckBoxAudioOnly.IsChecked == true)
                {
                    TextBloackQuality.Text = "Sound Quality";
                    var audioStreams = _streamManifest.GetAudioStreams().OrderByDescending(s => s.Bitrate);
                    ComboBoxQuality.ItemsSource = audioStreams.Select(s =>
                    {
                        string quality;
                        if (s.Bitrate.KiloBitsPerSecond < 128)
                            quality = "Low Quality";
                        else if (s.Bitrate.KiloBitsPerSecond < 256)
                            quality = "Medium Quality";
                        else
                            quality = "High Quality";

                        return $"{quality} - {s.AudioCodec}";
                    });
                    ComboBoxQuality.SelectedIndex = 0;

                }
                else
                {
                    TextBloackQuality.Text = "Video Quality";
                    var videoStreams = _streamManifest.GetVideoStreams().OrderByDescending(s => s.VideoQuality);
                    ComboBoxQuality.ItemsSource = videoStreams.Select(s => $"{s.VideoQuality.Label} - {(s.Bitrate.KiloBitsPerSecond):F2} Kbps");
                    ComboBoxQuality.SelectedIndex = 0;
                }
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

        private async Task DownloadAudioOnly(string videoId)
        {
            if (_streamManifest == null)
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Please fetch video details first."));
                return;
            }
            var audioStreams = _streamManifest.GetAudioStreams().OrderByDescending(s => s.Bitrate).ToList();
            if (ComboBoxQuality.SelectedIndex < 0 || ComboBoxQuality.SelectedIndex >= audioStreams.Count)
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Please select a valid sound quality."));
                return;
            }
            var selectedAudioStream = audioStreams[ComboBoxQuality.SelectedIndex];
            string savePath = Path.Combine(
                Path.GetDirectoryName(App.selectedSavePath),
                Path.GetFileNameWithoutExtension(App.selectedSavePath) + "_audio.mp3"
            );
            try
            {
                await DownloadStream(selectedAudioStream, savePath);

                if (App.isExplorerOpen)
                {
                    OpenExplorerIfNotRunning(savePath);
                    App.isExplorerOpen = false;
                }

                App.main.AddPage(new HomePage());
            }
            catch (Exception ex)
            {
                ShowWindowMessage(new Components.MessageBoxDanger($"Failed to download audio: {ex.Message}"));
            }
        }

        private async Task DownloadStream(IStreamInfo streamInfo, string savePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));

            await _youtube.Videos.Streams.DownloadAsync(streamInfo, savePath, new Progress<double>(progress =>
            {
                ProgressBarDownload.Value = progress * 100;
                TextBlockPercent.Text = $"{(progress * 100):F2}%";
            }));

            ShowWindowMessage(new Components.MessageBoxSuccess());
        }

        private async Task DownloadVideoAndAudio(string videoId)
        {
            if (_streamManifest == null)
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Please fetch video details first."));
                return;
            }
            string sanitizedTitle = SanitizeFileName(TextBoxTitle.Text);

            string selectedQualityLabel = ComboBoxQuality.SelectedItem.ToString().Split(' ')[0]; // Extract the quality label part

            var videoStream = _streamManifest.GetVideoStreams()
                .FirstOrDefault(s => s.VideoQuality.Label == selectedQualityLabel);

            if (videoStream == null)
            {
                ShowWindowMessage(new Components.MessageBoxDanger("Failed to find the selected video quality stream."));
                return;
            }

            var audioStream = _streamManifest.GetAudioStreams().GetWithHighestBitrate();

            string outputPath = App.selectedSavePath;

            string tempVideoPath = Path.Combine(Path.GetTempPath(), $"{sanitizedTitle}_video.mp4");
            string tempAudioPath = Path.Combine(Path.GetTempPath(), $"{sanitizedTitle}_audio.mp4");

            double videoProgress = 0;
            double audioProgress = 0;
            double percentage = 0;

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

            await Task.WhenAll(videoDownloadTask, audioDownloadTask);

            await Task.Run(() => MergeVideoAndAudio(tempVideoPath, tempAudioPath, outputPath));

            if (File.Exists(tempVideoPath)) File.Delete(tempVideoPath);
            if (File.Exists(tempAudioPath)) File.Delete(tempAudioPath);

            ShowWindowMessage(new Components.MessageBoxSuccess());

            if (App.isExplorerOpen)
            {
                OpenExplorerIfNotRunning(outputPath);
                App.isExplorerOpen = false;
            }
            App.main.AddPage(new HomePage());
        }

        private void MergeVideoAndAudio(string videoPath, string audioPath, string outputPath)
        {
            string ffmpegPath = Directory.GetCurrentDirectory() + "\\ffmpeg\\ffmpeg.exe"; // Make sure FFmpeg is installed and its path is provided
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
                FileName = "tg://resolve?domain=monyakkhara",
                UseShellExecute = true
            });
        }

        private async void CheckBoxAudioOnly_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (CheckBoxMp4 == null || CheckBoxAudioOnly == null)
            {
                return;
            }                               

            if (sender is not CheckBox checkBox)
                return;

            if (CheckBoxMp4.IsChecked == false && CheckBoxAudioOnly.IsChecked == false)
            {
                CheckBoxMp4.IsChecked = true;
                return;
            }

            StackPanelVideoDetail.Visibility = Visibility.Hidden;
            ImageLoading.Visibility = Visibility.Visible;

            if (checkBox.IsChecked == true)
            {
                if (checkBox == CheckBoxAudioOnly)
                    CheckBoxMp4.IsChecked = false;
                else if (checkBox == CheckBoxMp4)
                    CheckBoxAudioOnly.IsChecked = false;
            }

            try
            {
                await FetchVideoDetails();
            }
            finally
            {
                StackPanelVideoDetail.Visibility = Visibility.Visible;
                ImageLoading.Visibility = Visibility.Hidden;
            }
        }




        private void TextBoxTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(App.selectedSavePath))
            {
                string directory = Path.GetDirectoryName(App.selectedSavePath);
                string extension = Path.GetExtension(App.selectedSavePath);

                App.selectedSavePath = Path.Combine(directory, SanitizeFileName(TextBoxTitle.Text));
                TextBoxSavePath.Text = App.selectedSavePath; // Update the UI to reflect the new path
            }
        }

        private string SanitizeFileName(string fileName)
        {
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
                await FetchVideoDetails();
            }
        }

        private void SetTimer(int intervalMilliseconds, Action action)
        {
            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(intervalMilliseconds)
            };

            timer.Tick += (sender, e) =>
            {
                timer.Stop();

                action?.Invoke();
            };

            timer.Start();
        }

        private string ExtractVideoId(string url)
        {
            var regex = new Regex(@"(?:https?://)?(?:www\.)?(?:youtube\.com/watch\?v=|youtu\.be/)([a-zA-Z0-9_-]{11})");
            var match = regex.Match(url);
            return match.Success ? match.Groups[1].Value : null;
        }

        public static void ShowWindowMessage(Window windowToShow, int topOffset = 0, int autoCloseDelay = 3000)
        {
            if (App.main != null)
            {
                windowToShow.WindowStartupLocation = WindowStartupLocation.Manual; // Manual positioning
                windowToShow.Left = App.main.Left + (App.main.Width - windowToShow.Width) / 2; // Center horizontally
                windowToShow.Top = App.main.Top + topOffset; // Add top offset if needed

                windowToShow.Show();

                CloseWindowAfterDelay(windowToShow, autoCloseDelay);
            }
        }

        private static async void CloseWindowAfterDelay(Window windowToClose, int delayInMilliseconds)
        {
            await Task.Delay(delayInMilliseconds);
            windowToClose.Close(); // Close the window after the delay
        }


        public void OpenExplorerIfNotRunning(string outputPath)
        {
            string directoryPath = Path.GetDirectoryName(outputPath);
            Process.Start("explorer.exe", $"/select,\"{outputPath}\"");
        }

    }
}
