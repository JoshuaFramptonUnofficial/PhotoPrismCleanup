using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoPrismCleanup
{
    public class MediaItem
    {
        public BitmapImage Thumbnail { get; set; } = null!;
        public bool IsVideo { get; set; }
        public string FileName { get; set; } = "";
        public string FullPath { get; set; } = "";
    }

    public partial class SummaryWindow : Window
    {
        private readonly List<string> _paths;
        private readonly PhotoPrismService _service;
        private readonly List<MediaItem> _items = new();

        public SummaryWindow(List<string> paths, PhotoPrismService service)
        {
            InitializeComponent();
            _paths = paths;
            _service = service;
            Lbl.Text = $"You have {_paths.Count} items queued for deletion.";
            LoadThumbnails();
        }

        private async void LoadThumbnails()
        {
            foreach (var path in _paths)
            {
                var item = new MediaItem
                {
                    FullPath = path,
                    FileName = Path.GetFileName(path),
                    IsVideo = PhotoPrismService.VideoExts.Contains(Path.GetExtension(path).ToLowerInvariant())
                };
                try
                {
                    if (!item.IsVideo)
                    {
                        var data = await Task.Run(() => _service.DownloadToMemory(path));
                        var bmp = new BitmapImage();
                        using var ms = new MemoryStream(data);
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.DecodePixelWidth = 100;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        item.Thumbnail = bmp;
                    }
                    else
                    {
                        // placeholder icon
                        item.Thumbnail = new BitmapImage(
                            new Uri("pack://application:,,,/Icons/logo.ico"));
                    }
                }
                catch { /* skip errors */ }

                _items.Add(item);
            }
            ThumbList.ItemsSource = _items;
        }

        private async void OK_Click(object sender, RoutedEventArgs e)
        {
            OK.IsEnabled = Cancel.IsEnabled = false;
            DelProgressBar.Visibility = Visibility.Visible;

            int total = _paths.Count;
            int success = 0;

            for (int i = 0; i < total; i++)
            {
                var path = _paths[i];
                if (DownloadBefore.IsChecked == true)
                {
                    var local = Path.Combine(Path.GetTempPath(), Path.GetFileName(path));
                    await Task.Run(() => _service.DownloadToFile(path, local));
                }

                var failed = await Task.Run(() => _service.DeleteFiles(new[] { path }));
                if (!failed.Any()) success++;
                DelProgressBar.Value = (double)(i + 1) / total;
            }

            ResultText.Visibility = Visibility.Visible;
            ResultText.Text = $"Deleted {success} of {total} items.";
            CloseBtn.Visibility = Visibility.Visible;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
