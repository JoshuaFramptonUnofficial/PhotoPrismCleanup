using Microsoft.Win32;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;

namespace PhotoPrismCleanup
{
    public partial class MainWindow : Window
    {
        private readonly AppConfig _cfg;
        private readonly PhotoPrismService _svc;
        private List<string> _allMedia = new();
        private List<string> _mediaList = new();
        private readonly List<string> _toDelete;
        private int _index;
        private string? _currentTempVideo;
        private double _videoRotation = 0;

        public MainWindow()
        {
            InitializeComponent();

            // Load config + theme
            _cfg = ConfigService.Load();
            App.ApplyTheme(_cfg.Theme);
            ThemeBtn.Content = _cfg.Theme == ThemeMode.Dark ? "🌜" : "🌞";

            // Populate Connect form
            HostBox.Text = _cfg.Host;
            PortBox.Text = _cfg.Port.ToString();
            UserBox.Text = _cfg.Username;
            UseKeyBox.IsChecked = _cfg.UseKey;
            KeyBox.Text = _cfg.KeyPath;
            PwdBox.Password = _cfg.UseKey ? "" : _cfg.PasswordOrKey;
            FolderBox.Text = _cfg.RemoteFolder;
            ThumbCacheBox.Text = _cfg.ThumbCacheFolder;
            ImportFolderBox.Text = _cfg.ImportFolder;

            // Populate Settings tab
            SettingsFolderBox.Text = _cfg.RemoteFolder;
            SettingsThumbCacheBox.Text = _cfg.ThumbCacheFolder;
            SettingsImportFolderBox.Text = _cfg.ImportFolder;
            SettingsShowPhotosBox.IsChecked = _cfg.ShowPhotos;
            SettingsShowVideosBox.IsChecked = _cfg.ShowVideos;

            _toDelete = new List<string>(_cfg.PendingDeletes);
            _svc = new PhotoPrismService(
                _cfg.RemoteFolder,
                _cfg.ThumbCacheFolder,
                _cfg.ImportFolder
            );
        }

        private void BrowseKey_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select private key",
                Filter = "Key files|*.*"
            };
            if (dlg.ShowDialog() == true)
                KeyBox.Text = dlg.FileName;
        }

        private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save login settings
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
                    _cfg.PasswordOrKey, _cfg.UseKey, _cfg.KeyPath));

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
                await ShowCurrentMedia();
            }
            catch (SocketException)
            {
                MessageBox.Show("Cannot reach host.", "Connection Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Not connected";
            }
            catch (SftpPathNotFoundException)
            {
                MessageBox.Show("Remote folder not found.", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Not connected";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed:\n{ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Not connected";
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e) => _ = DeleteCurrent();
        private void UndoBtn_Click(object sender, RoutedEventArgs e) => _ = Undo();
        private void KeepBtn_Click(object sender, RoutedEventArgs e) => _ = KeepCurrent();
        private void RotateBtn_Click(object sender, RoutedEventArgs e) => RotateCurrentVideo();

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (MainTabs.Visibility != Visibility.Visible) return;
            if (e.Key == Key.Left) { e.Handled = true; _ = DeleteCurrent(); }
            else if (e.Key == Key.Z || e.Key == Key.Down)
            { e.Handled = true; _ = Undo(); }
            else if (e.Key == Key.Right) { e.Handled = true; _ = KeepCurrent(); }
            else if (e.Key == Key.R) { e.Handled = true; RotateCurrentVideo(); }
        }

        private async Task DeleteCurrent()
        {
            _toDelete.Add(_mediaList[_index]);
            await NextItem();
        }

        private async Task KeepCurrent()
        {
            await NextItem();
        }

        private async Task Undo()
        {
            if (_index <= 0) { System.Media.SystemSounds.Beep.Play(); return; }
            _index--;
            _toDelete.Remove(_mediaList[_index]);
            _cfg.LastIndex = _index;
            _cfg.PendingDeletes = new List<string>(_toDelete);
            ConfigService.Save(_cfg);
            await ShowCurrentMedia();
        }

        private void RotateCurrentVideo()
        {
            if (SwipeVideo.Visibility != Visibility.Visible) return;
            _videoRotation = (_videoRotation + 90) % 360;
            SwipeVideo.LayoutTransform = new RotateTransform(_videoRotation);
        }

        private async Task NextItem()
        {
            // make sure video stops
            SwipeVideo.Stop();

            _cfg.LastIndex = ++_index;
            _cfg.PendingDeletes = new List<string>(_toDelete);
            ConfigService.Save(_cfg);

            if (_index >= _mediaList.Count)
            {
                var dlg = new SummaryWindow(_toDelete, _svc) { Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    _toDelete.Clear();
                    _cfg.PendingDeletes.Clear();
                    _cfg.LastIndex = 0;
                    ConfigService.Save(_cfg);
                    _allMedia = await Task.Run(() => _svc.ListAllMedia());
                    BuildFilteredList();
                }
                _index = 0;
            }

            await ShowCurrentMedia();
        }

        private void BuildFilteredList()
        {
            _mediaList = _allMedia
              .Where(p =>
                (PhotoPrismService.ImageExts.Contains(Path.GetExtension(p).ToLower()) && _cfg.ShowPhotos)
             || (PhotoPrismService.VideoExts.Contains(Path.GetExtension(p).ToLower()) && _cfg.ShowVideos)
              ).ToList();
        }

        private async Task ShowCurrentMedia()
        {
            SwipeVideo.Stop();
            LoadingOverlay.Visibility = Visibility.Visible;
            SwipeImage.Visibility = Visibility.Collapsed;
            SwipeVideo.Visibility = Visibility.Collapsed;

            if (_currentTempVideo != null)
            {
                try { File.Delete(_currentTempVideo); } catch { }
                _currentTempVideo = null;
            }

            var path = _mediaList[_index];
            var ext = Path.GetExtension(path).ToLowerInvariant();

            try
            {
                if (PhotoPrismService.ImageExts.Contains(ext))
                {
                    var data = await Task.Run(() => _svc.DownloadToMemory(path));
                    var bmp = new BitmapImage();
                    using var ms = new MemoryStream(data);
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();

                    SwipeImage.Source = bmp;
                    SwipeImage.Visibility = Visibility.Visible;
                }
                else
                {
                    _currentTempVideo = await Task.Run(() => _svc.DownloadToTemp(path));
                    SwipeVideo.Source = new Uri(_currentTempVideo);
                    SwipeVideo.Visibility = Visibility.Visible;
                    SwipeVideo.Play();
                    SwipeVideo.LayoutTransform = new RotateTransform(_videoRotation);
                }

                DeleteProgressBar.Visibility = Visibility.Visible;
                DeleteProgressBar.Minimum = 0;
                DeleteProgressBar.Maximum = _mediaList.Count - 1;
                DeleteProgressBar.Value = _index;

                StatusText.Text = $"Viewing {_index + 1} / {_mediaList.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading media:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void ThemeBtn_Click(object sender, RoutedEventArgs e)
        {
            _cfg.Theme = _cfg.Theme == ThemeMode.Dark
                       ? ThemeMode.Light
                       : ThemeMode.Dark;
            ConfigService.Save(_cfg);
            App.ApplyTheme(_cfg.Theme);
            ThemeBtn.Content = _cfg.Theme == ThemeMode.Dark ? "🌜" : "🌞";
        }

        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "help.html");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            { UseShellExecute = true });
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            _cfg.RemoteFolder = SettingsFolderBox.Text.Trim();
            _cfg.ThumbCacheFolder = SettingsThumbCacheBox.Text.Trim();
            _cfg.ImportFolder = SettingsImportFolderBox.Text.Trim();
            _cfg.ShowPhotos = SettingsShowPhotosBox.IsChecked == true;
            _cfg.ShowVideos = SettingsShowVideosBox.IsChecked == true;
            ConfigService.Save(_cfg);

            MessageBox.Show("Settings saved.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            BuildFilteredList();
            _index = Math.Min(_cfg.LastIndex, _mediaList.Count - 1);
            _ = ShowCurrentMedia();
        }

        private void SaveProgress_Click(object sender, RoutedEventArgs e)
        {
            _cfg.PendingDeletes = new List<string>(_toDelete);
            ConfigService.Save(_cfg);
            MessageBox.Show("Progress saved.",
                            "OK", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _svc.ClearThumbnailCache();
                MessageBox.Show("Thumbnail cache cleared.",
                                "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear cache:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportPhotos_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select media to import",
                Multiselect = true,
                Filter = "Media files|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.mp4;*.mov;*.avi;*.mkv;*.webm|All|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                var preview = new ImportPreviewWindow(dlg.FileNames, _svc) { Owner = this };
                preview.ShowDialog();
            }
        }

        private void BulkDeleteNow_Click(object sender, RoutedEventArgs e)
        {
            if (_toDelete.Count == 0)
            {
                MessageBox.Show("No items marked for deletion.",
                                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new SummaryWindow(_toDelete, _svc) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _toDelete.Clear();
                _cfg.PendingDeletes.Clear();
                ConfigService.Save(_cfg);
                _ = ShowCurrentMedia();
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            _svc.Dispose();
            ConfigService.Save(new AppConfig());
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
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
