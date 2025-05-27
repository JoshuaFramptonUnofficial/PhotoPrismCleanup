using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoPrismCleanup
{
    public partial class ImportPreviewWindow : Window
    {
        private readonly string[] _files;
        private readonly PhotoPrismService _svc;

        public ImportPreviewWindow(string[] files, PhotoPrismService svc)
        {
            InitializeComponent();
            _files = files;
            _svc = svc;

            var items = new List<ThumbItem>();
            foreach (var f in _files)
            {
                try
                {
                    var data = File.ReadAllBytes(f);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = new MemoryStream(data);
                    bmp.EndInit();
                    bmp.Freeze();

                    bool isVid = PhotoPrismService.VideoExts
                                .Contains(Path.GetExtension(f)
                                .ToLowerInvariant());
                    items.Add(new ThumbItem { Thumb = bmp, Path = f, IsVideo = isVid });
                }
                catch { }
            }
            PreviewList.ItemsSource = items;
            ImportProgressBar.Maximum = items.Count;
            ProgressText.Text = $"0 / {items.Count} transferred";
        }

        private void OnThumbClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string path)
            {
                if (PhotoPrismService.VideoExts
                    .Contains(Path.GetExtension(path).ToLowerInvariant()))
                {
                    var win = new VideoPreviewWindow(path) { Owner = this };
                    win.ShowDialog();
                }
                else
                {
                    Process.Start(new ProcessStartInfo(path)
                    { UseShellExecute = true });
                }
            }
        }

        private async void ConfirmBtn_Click(object s, RoutedEventArgs e)
        {
            ConfirmBtn.IsEnabled = false;
            CancelBtn.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;

            int count = 0;
            foreach (var path in _files)
            {
                await Task.Run(() => _svc.ImportFiles(new[] { path }));
                count++;
                ImportProgressBar.Value = count;
                ProgressText.Text = $"{count} / {_files.Length} transferred";
            }

            MessageBox.Show(
                "Import finished.\n\nPlease open PhotoPrism to let it index the new files.",
                "Import Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object s, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private class ThumbItem
        {
            public BitmapImage Thumb { get; set; }
            public string Path { get; set; }
            public bool IsVideo { get; set; }
        }
    }
}
