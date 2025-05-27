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
        private List<string> _toDelete;
        private int _index;
        private string? _currentTempVideo;

        public MainWindow()
        {
            InitializeComponent();
            _currentTempVideo = null;

            // Load saved config
            _cfg = ConfigService.Load();
            HostBox.Text = _cfg.Host;
            PortBox.Text = _cfg.Port.ToString();
            UserBox.Text = _cfg.Username;
            UseKeyBox.IsChecked = _cfg.UseKey;
            KeyBox.Text = _cfg.KeyPath;
            PwdBox.Password = _cfg.UseKey ? "" : _cfg.PasswordOrKey;
            FolderBox.Text = _cfg.RemoteFolder;
            ThumbCacheBox.Text = _cfg.ThumbCacheFolder;
            ImportFolderBox.Text = _cfg.ImportFolder;
            ShowPhotosBox.IsChecked = _cfg.ShowPhotos;
            ShowVideosBox.IsChecked = _cfg.ShowVideos;
            _toDelete = new List<string>(_cfg.PendingDeletes);

            // Set emoji theme button
            ThemeBtn.Content = _cfg.Theme == ThemeMode.Dark ? "🌜" : "🌞";

            // *** HERE: pass three folders into the service ***
            _svc = new PhotoPrismService(
                _cfg.RemoteFolder,
                _cfg.ThumbCacheFolder,
                _cfg.ImportFolder
            );
        }

        private void ThemeBtn_Click(object sender, RoutedEventArgs e)
        {
            _cfg.Theme = _cfg.Theme == ThemeMode.Dark
                       ? ThemeMode.Light
                       : ThemeMode.Dark;
            ConfigService.Save(_cfg);
            ThemeBtn.Content = _cfg.Theme == ThemeMode.Dark ? "🌜" : "🌞";
        }

        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "help.html");
                Process.Start(new ProcessStartInfo(path)
                { UseShellExecute = true });
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
                  var ext = Path.GetExtension(p).ToLowerInvariant();
                  bool isImg = PhotoPrismService.ImageExts.Contains(ext);
                  bool isVid = PhotoPrismService.VideoExts.Contains(ext);
                  return (isImg && _cfg.ShowPhotos)
                      || (isVid && _cfg.ShowVideos);
              })
              .ToList();
        }

        private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save current connection/settings
                _cfg.Host = HostBox.Text.Trim();
                _cfg.Port = int.TryParse(PortBox.Text, out var p) ? p : 22;
                _cfg.Username = UserBox.Text.Trim();
                _cfg.UseKey = UseKeyBox.IsChecked == true;
                _cfg.KeyPath = KeyBox.Text.Trim();
                _cfg.PasswordOrKey = _cfg.UseKey ? "" : PwdBox.Password;
                _cfg.RemoteFolder = FolderBox.Text.Trim();
                _cfg.ThumbCacheFolder = ThumbCacheBox.Text.Trim();
                _cfg.ImportFolder = ImportFolderBox.Text.Trim();
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
                                "Connection Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (SftpPathNotFoundException)
            {
                MessageBox.Show("Remote folder not found. Check path.",
                                "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed:\n{ex.Message}",
                                "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
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

                var path = _mediaList[_index];
                var ext = Path.GetExtension(path).ToLowerInvariant();

                if (PhotoPrismService.ImageExts.Contains(ext))
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
                // videos preview in import dialog, not here

                ProgressText.Text = $"Item {_index + 1} / {_mediaList.Count}";
                StatusText.Text = "Connected";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load failed:\n{ex.Message}",
                                "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async Task NavigateAsync(bool delete)
        {
            if (delete) _toDelete.Add(_mediaList[_index]);
            _index++;
            _cfg.LastIndex = _index;
            _cfg.PendingDeletes = new List<string>(_toDelete);
            ConfigService.Save(_cfg);

            if (_index >= _mediaList.Count)
            {
                var dlg = new SummaryWindow(_toDelete, _svc) { Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    _allMedia = await Task.Run(() => _svc.ListAllMedia());
                    BuildFilteredList();
                }
                _toDelete.Clear();
                _cfg.PendingDeletes.Clear();
                _cfg.LastIndex = 0;
                ConfigService.Save(_cfg);
                _index = 0;
            }

            await LoadCurrentAsync();
        }

        private void DelBtn_Click(object s, RoutedEventArgs e) => _ = NavigateAsync(true);
        private void KeepBtn_Click(object s, RoutedEventArgs e) => _ = NavigateAsync(false);

        private async void UndoBtn_Click(object s, RoutedEventArgs e)
        {
            if (_index <= 0)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }
            _index--;
            _toDelete.Remove(_mediaList[_index]);
            _cfg.LastIndex = _index;
            _cfg.PendingDeletes = new List<string>(_toDelete);
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

        private void BulkDeleteNow_Click(object s, RoutedEventArgs e)
        {
            var dlg = new SummaryWindow(_toDelete, _svc) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _allMedia = _svc.ListAllMedia();
                BuildFilteredList();
                _toDelete.Clear();
                _cfg.PendingDeletes.Clear();
                ConfigService.Save(_cfg);
                _index = 0;
                _ = LoadCurrentAsync();
            }
        }

        private void ClearCache_Click(object s, RoutedEventArgs e)
        {
            try
            {
                _svc.ClearThumbnailCache();
                MessageBox.Show("Thumbnail cache cleared.",
                                "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Clear cache failed:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportPhotos_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select media to import",
                Multiselect = true,
                Filter = "Media files|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.mp4;*.mov;*.avi;*.mkv;*.webm|All|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            var preview = new ImportPreviewWindow(dlg.FileNames, _svc)
            { Owner = this };
            preview.ShowDialog();
        }

        private void SaveProgress_Click(object s, RoutedEventArgs e)
        {
            _cfg.PendingDeletes = new List<string>(_toDelete);
            ConfigService.Save(_cfg);
            MessageBox.Show("Progress (including pending deletes) saved.",
                            "OK", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveSettings_Click(object s, RoutedEventArgs e)
        {
            _cfg.RemoteFolder = FolderBox.Text.Trim();
            _cfg.ThumbCacheFolder = ThumbCacheBox.Text.Trim();
            _cfg.ImportFolder = ImportFolderBox.Text.Trim();
            _cfg.ShowPhotos = ShowPhotosBox.IsChecked == true;
            _cfg.ShowVideos = ShowVideosBox.IsChecked == true;
            _cfg.PendingDeletes = new List<string>(_toDelete);
            ConfigService.Save(_cfg);

            MessageBox.Show("Settings applied.", "OK",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            _allMedia = _svc.ListAllMedia();
            BuildFilteredList();
            _index = Math.Min(_cfg.LastIndex, _mediaList.Count - 1);
            _ = LoadCurrentAsync();
        }

        private void Logout_Click(object s, RoutedEventArgs e)
        {
            var cfgPath = Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
              "PhotoPrismCleanup", "config.json");
            if (File.Exists(cfgPath)) File.Delete(cfgPath);
            Process.Start(new ProcessStartInfo(
                AppDomain.CurrentDomain.FriendlyName)
            { UseShellExecute = true });
            Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _cfg.PendingDeletes = new List<string>(_toDelete);
            ConfigService.Save(_cfg);
            _svc.Dispose();
            base.OnClosing(e);
        }
    }
}
