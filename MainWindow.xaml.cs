using Microsoft.Win32;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PhotoPrismCleanup
{
    public partial class MainWindow : Window
    {
        private readonly AppConfig _cfg;
        private readonly PhotoPrismService _svc;
        private List<string> _allMedia = new();
        private List<string> _mediaList = new();
        private readonly List<string> _toDelete = new();
        private int _index;
        private string? _currentTempVideo;

        public MainWindow()
        {
            InitializeComponent();
            _currentTempVideo = null;

            // Load config into UI
            _cfg = ConfigService.Load();
            HostBox.Text = _cfg.Host;
            PortBox.Text = _cfg.Port.ToString();
            UserBox.Text = _cfg.Username;
            UseKeyBox.IsChecked = _cfg.UseKey;
            KeyBox.Text = _cfg.KeyPath;
            PwdBox.Password = _cfg.UseKey ? "" : _cfg.PasswordOrKey;
            FolderBox.Text = _cfg.RemoteFolder;
            ThumbCacheBox.Text = _cfg.ThumbCacheFolder;
            ShowPhotosBox.IsChecked = _cfg.ShowPhotos;
            ShowVideosBox.IsChecked = _cfg.ShowVideos;

            // Theme toggle
            bool isDark = _cfg.Theme == ThemeMode.Dark
                       || (_cfg.Theme == ThemeMode.System && SystemParameters.HighContrast);
            ThemeToggle.IsChecked = isDark;
            ThemeIcon.Text = isDark ? "🌜" : "🌞";
            ApplyTheme();

            _svc = new PhotoPrismService(
                     _cfg.RemoteFolder,
                     _cfg.ThumbCacheFolder);
        }

        private void ApplyTheme()
        {
            var dicts = Application.Current.Resources.MergedDictionaries;
            dicts.Clear();
            if (ThemeToggle.IsChecked == true)
                dicts.Add(new ResourceDictionary { Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative) });
            else
                dicts.Add(new ResourceDictionary { Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative) });
        }

        private void ThemeToggle_Checked(object s, RoutedEventArgs e)
        {
            try
            {
                _cfg.Theme = ThemeMode.Dark;
                ApplyTheme();
                ThemeIcon.Text = "🌜";
                ConfigService.Save(_cfg);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to switch theme:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ThemeToggle_Unchecked(object s, RoutedEventArgs e)
        {
            try
            {
                _cfg.Theme = ThemeMode.Light;
                ApplyTheme();
                ThemeIcon.Text = "🌞";
                ConfigService.Save(_cfg);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to switch theme:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "help.html");
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open Help:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildFilteredList()
        {
            _mediaList = _allMedia
              .Where(p =>
              {
                  var ext = System.IO.Path.GetExtension(p).ToLowerInvariant();
                  bool isImg = Array.Exists(PhotoPrismService.ImageExts, e => e == ext);
                  bool isVid = Array.Exists(PhotoPrismService.VideoExts, e => e == ext);
                  return (isImg && _cfg.ShowPhotos) || (isVid && _cfg.ShowVideos);
              })
              .ToList();
        }

        private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save login info
                _cfg.Host = HostBox.Text.Trim();
                _cfg.Port = int.TryParse(PortBox.Text, out var p) ? p : 22;
                _cfg.Username = UserBox.Text.Trim();
                _cfg.UseKey = UseKeyBox.IsChecked == true;
                _cfg.KeyPath = KeyBox.Text.Trim();
                _cfg.PasswordOrKey = _cfg.UseKey ? "" : PwdBox.Password;
                ConfigService.Save(_cfg);

                StatusText.Text = "Connecting…";
                await Task.Run(() => _svc.Connect(
                    _cfg.Host, _cfg.Port, _cfg.Username,
                    _cfg.UseKey ? "" : _cfg.PasswordOrKey,
                    _cfg.UseKey, _cfg.KeyPath));

                _allMedia = await Task.Run(() => _svc.ListAllMedia());
                BuildFilteredList();
                if (_mediaList.Count == 0)
                {
                    MessageBox.Show("No media found.", "Empty",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ConnectGrid.Visibility = Visibility.Collapsed;
                MainTabs.Visibility = Visibility.Visible;

                _index = Math.Min(_cfg.LastIndex, _mediaList.Count - 1);
                await LoadCurrentAsync();
            }
            catch (SocketException)
            {
                MessageBox.Show("Cannot reach host. Check network/SSH settings.",
                                "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (SftpPathNotFoundException)
            {
                MessageBox.Show("Remote folder not found. Check path.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadCurrentAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                PhotoImg.Visibility = Visibility.Collapsed;
                VideoPlayer.Visibility = Visibility.Collapsed;

                if (_currentTempVideo != null)
                {
                    VideoPlayer.Stop();
                    VideoPlayer.Source = null;
                    File.Delete(_currentTempVideo);
                    _currentTempVideo = null;
                }

                string path = _mediaList[_index];
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

                if (Array.Exists(PhotoPrismService.ImageExts, e => e == ext))
                {
                    var data = await Task.Run(() => _svc.DownloadToMemory(path));
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = new MemoryStream(data);
                    bmp.EndInit();
                    bmp.Freeze();

                    PhotoImg.Source = bmp;
                    PhotoImg.Visibility = Visibility.Visible;
                }
                else
                {
                    string tmp = System.IO.Path.Combine(
                        Path.GetTempPath(), Guid.NewGuid() + ext);
                    await Task.Run(() => _svc.DownloadToFile(path, tmp));
                    _currentTempVideo = tmp;
                    VideoPlayer.Source = new Uri(tmp);
                    VideoPlayer.Visibility = Visibility.Visible;
                    VideoPlayer.Play();
                }

                ProgressText.Text = $"Item {_index + 1} / {_mediaList.Count}";
                StatusText.Text = "Connected";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load media:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async Task NavigateAsync(bool delete)
        {
            try
            {
                if (delete) _toDelete.Add(_mediaList[_index]);
                _index++;
                _cfg.LastIndex = _index;
                ConfigService.Save(_cfg);

                if (_index >= _mediaList.Count)
                {
                    var dlg = new SummaryWindow(_toDelete, _svc) { Owner = this };
                    bool? res = dlg.ShowDialog();
                    if (res == true)
                    {
                        _allMedia = await Task.Run(() => _svc.ListAllMedia());
                        BuildFilteredList();
                    }
                    _toDelete.Clear();
                    _index = 0;
                    _cfg.LastIndex = 0;
                    ConfigService.Save(_cfg);
                }

                await LoadCurrentAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DelBtn_Click(object s, RoutedEventArgs e) => _ = NavigateAsync(true);
        private void KeepBtn_Click(object s, RoutedEventArgs e) => _ = NavigateAsync(false);

        private async void UndoBtn_Click(object s, RoutedEventArgs e)
        {
            if (_index <= 0) { System.Media.SystemSounds.Beep.Play(); return; }
            _index--;
            _toDelete.Remove(_mediaList[_index]);
            _cfg.LastIndex = _index;
            ConfigService.Save(_cfg);
            await LoadCurrentAsync();
        }

        private void Window_PreviewKeyDown(object s, KeyEventArgs e)
        {
            if (MainTabs.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Left) { e.Handled = true; _ = NavigateAsync(true); }
                else if (e.Key == Key.Right) { e.Handled = true; _ = NavigateAsync(false); }
                else if (e.Key == Key.Z || e.Key == Key.Down)
                { e.Handled = true; UndoBtn_Click(s, null); }
            }
        }

        private async void BulkDeleteNow_Click(object s, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SummaryWindow(_toDelete, _svc) { Owner = this };
                bool? res = dlg.ShowDialog();
                if (res == true)
                {
                    _allMedia = await Task.Run(() => _svc.ListAllMedia());
                    BuildFilteredList();
                    _index = Math.Min(_cfg.LastIndex, _mediaList.Count - 1);
                    await LoadCurrentAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bulk delete failed:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearCache_Click(object s, RoutedEventArgs e)
        {
            try
            {
                _svc.ClearThumbnailCache();
                MessageBox.Show("Thumbnail cache cleared.", "OK",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Clear cache failed:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportPhotos_Click(object s, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Select photos/videos to import",
                    Multiselect = true,
                    Filter = "Media files|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.mp4;*.mov;*.avi;*.mkv;*.webm|All|*.*"
                };
                if (dlg.ShowDialog(this) == true)
                {
                    _svc.ImportFiles(dlg.FileNames);
                    MessageBox.Show("Import complete.", "OK",
                                    MessageBoxButton.OK, MessageBoxImage.Information);

                    // refresh feed
                    _allMedia = _svc.ListAllMedia();
                    BuildFilteredList();
                    _index = Math.Min(_cfg.LastIndex, _mediaList.Count - 1);
                    _ = LoadCurrentAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProgress_Click(object s, RoutedEventArgs e)
        {
            try
            {
                ConfigService.Save(_cfg);
                MessageBox.Show("Progress saved.", "OK",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save progress failed:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSettings_Click(object s, RoutedEventArgs e)
        {
            try
            {
                _cfg.RemoteFolder = FolderBox.Text.Trim();
                _cfg.ThumbCacheFolder = ThumbCacheBox.Text.Trim();
                _cfg.ShowPhotos = ShowPhotosBox.IsChecked == true;
                _cfg.ShowVideos = ShowVideosBox.IsChecked == true;
                ConfigService.Save(_cfg);
                MessageBox.Show("Settings applied.", "OK",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                // refresh feed
                _allMedia = _svc.ListAllMedia();
                BuildFilteredList();
                _index = Math.Min(_cfg.LastIndex, _mediaList.Count - 1);
                _ = LoadCurrentAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Apply settings failed:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Logout_Click(object s, RoutedEventArgs e)
        {
            try
            {
                string cfgPath = Path.Combine(
                  Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                  "PhotoPrismCleanup", "config.json");
                if (File.Exists(cfgPath)) File.Delete(cfgPath);

                Process.Start(new ProcessStartInfo(
                    AppDomain.CurrentDomain.FriendlyName)
                { UseShellExecute = true });
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Logout failed:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            ConfigService.Save(_cfg);
            _svc.Dispose();
            base.OnClosing(e);
        }
    }
}
