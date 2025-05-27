using System;
using System.Collections.Generic;
using System.IO;
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

            // build thumbnail list
            var items = new List<object>();
            foreach (var f in files)
            {
                try
                {
                    byte[] data = File.ReadAllBytes(f);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = new MemoryStream(data);
                    bmp.EndInit();
                    bmp.Freeze();
                    items.Add(new { Thumb = bmp });
                }
                catch
                {
                    // skip unreadable files
                }
            }
            PreviewList.ItemsSource = items;

            ImportProgressBar.Maximum = items.Count;
            ProgressText.Text = $"0 / {items.Count} transferred";
        }

        private async void ConfirmBtn_Click(object sender, RoutedEventArgs e)
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

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
