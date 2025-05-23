using Microsoft.Win32;
using Renci.SshNet.Common;      // for SftpPathNotFoundException
using System;
using System.Collections.Generic;
using System.IO;
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
        private List<string> _mediaList = new();
        private readonly List<string> _toDelete = new();
        private int _index;
        private string? _currentTempVideo;

        public MainWindow()
        {
            InitializeComponent();
            _currentTempVideo = null;

            // load config
            _cfg = ConfigService.Load();
            HostBox.Text = _cfg.Host;
            PortBox.Text = _cfg.Port.ToString();
            UserBox.Text = _cfg.Username;
            UseKeyBox.IsChecked = _cfg.UseKey;
            KeyBox.Text = _cfg.KeyPath;
            PwdBox.Password = _cfg.UseKey ? "" : _cfg.PasswordOrKey;
            FolderBox.Text = _cfg.RemoteFolder;

            // adjust if your thumbnail-cache path differs
            _svc = new PhotoPrismService(
                     _cfg.RemoteFolder,
                     "/opt/photoprism/storage/cache/thumbnails"
                   );
        }

        private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            // save SSH settings & folder
            _cfg.Host = HostBox.Text.Trim();
            _cfg.Port = int.TryParse(PortBox.Text, out var p) ? p : 22;
            _cfg.Username = UserBox.Text.Trim();
            _cfg.UseKey = UseKeyBox.IsChecked == true;
            _cfg.KeyPath = KeyBox.Text.Trim();
            _cfg.PasswordOrKey = _cfg.UseKey ? "" : PwdBox.Password;
            _cfg.RemoteFolder = FolderBox.Text.Trim();
            ConfigService.Save(_cfg);

            StatusText.Text = "Connecting…";
            try
            {
                await Task.Run(() =>
                    _svc.Connect(
                        _cfg.Host, _cfg.Port, _cfg.Username,
                        _cfg.UseKey ? "" : _cfg.PasswordOrKey,
                        _cfg.UseKey, _cfg.KeyPath));
                _mediaList = await Task.Run(() => _svc.ListAllMedia());
            }
            catch (SftpPathNotFoundException)
            {
                MessageBox.Show("Remote folder not found.", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed:\n{ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_mediaList.Count == 0)
            {
                MessageBox.Show("No media found.", "Empty",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // swap to Review/Settings UI
            ConnectGrid.Visibility = Visibility.Collapsed;
            MainTabs.Visibility = Visibility.Visible;

            _index = Math.Min(_cfg.LastIndex, _mediaList.Count - 1);
            await LoadCurrentAsync();
        }

        private async Task LoadCurrentAsync()
        {
            // clean up last video temp file
            if (_currentTempVideo != null)
            {
                VideoPlayer.Stop();
                VideoPlayer.Source = null;
                File.Delete(_currentTempVideo);
                _currentTempVideo = null;
            }

            string path = _mediaList[_index];
            string ext = Path.GetExtension(path).ToLowerInvariant();

            PhotoImg.Visibility = Visibility.Collapsed;
            VideoPlayer.Visibility = Visibility.Collapsed;

            if (Array.Exists(PhotoPrismService.ImageExts, e => e == ext))
            {
                // image
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
                // video
                string tmp = Path.Combine(
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

        private async Task NavigateAsync(bool delete)
        {
            if (delete)
                _toDelete.Add(_mediaList[_index]);

            _index++;
            _cfg.LastIndex = _index;
            ConfigService.Save(_cfg);

            if (_index >= _mediaList.Count)
            {
                var dlg = new SummaryWindow(_toDelete, _svc) { Owner = this };
                bool? res = dlg.ShowDialog();

                if (res == true)
                    _mediaList = await Task.Run(() => _svc.ListAllMedia());

                _toDelete.Clear();
                _index = 0;
                _cfg.LastIndex = 0;
                ConfigService.Save(_cfg);
                await LoadCurrentAsync();
                return;
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
            ConfigService.Save(_cfg);
            await LoadCurrentAsync();
        }

        private void Window_PreviewKeyDown(object s, KeyEventArgs e)
        {
            if (MainTabs.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Left)
                { e.Handled = true; _ = NavigateAsync(true); }
                if (e.Key == Key.Right)
                { e.Handled = true; _ = NavigateAsync(false); }
                if (e.Key == Key.Z || e.Key == Key.Down)
                { e.Handled = true; UndoBtn_Click(s, null); }
            }
        }

        private void BulkDeleteNow_Click(object s, RoutedEventArgs e)
            => new SummaryWindow(_toDelete, _svc) { Owner = this }.ShowDialog();

        private void ClearCache_Click(object s, RoutedEventArgs e)
        {
            try
            {
                _svc.ClearThumbnailCache();
                MessageBox.Show("Thumbnail cache cleared.", "OK",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show("Failed to clear thumbnail cache.", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportPhotos_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select photos/videos to import",
                Multiselect = true,
                Filter = "Media files|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.mp4;*.mov;*.avi;*.mkv;*.webm|All|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                try
                {
                    _svc.ImportFiles(dlg.FileNames);
                    MessageBox.Show("Import complete.", "OK",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Import failed:\n{ex.Message}", "Error",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveSettings_Click(object s, RoutedEventArgs e)
        {
            _cfg.RemoteFolder = FolderBox.Text.Trim();
            ConfigService.Save(_cfg);
            MessageBox.Show("Settings saved.", "OK",
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Logout_Click(object s, RoutedEventArgs e)
        {
            var cfgPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PhotoPrismCleanup", "config.json");
            if (File.Exists(cfgPath))
                File.Delete(cfgPath);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppDomain.CurrentDomain.FriendlyName,
                UseShellExecute = true
            });
            Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            ConfigService.Save(_cfg);
            _svc.Dispose();
            base.OnClosing(e);
        }
    }
}
