using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PhotoPrismCleanup
{
    public partial class SummaryWindow : Window
    {
        private readonly List<string> _files;
        private readonly PhotoPrismService _svc;
        private bool _didDelete;

        public SummaryWindow(List<string> files, PhotoPrismService svc)
        {
            InitializeComponent();
            _files = files;
            _svc = svc;

            // set button text
            OK.Content = $"Delete {_files.Count} item{(_files.Count > 1 ? "s" : "")}";
            Lbl.Text = $"You selected {_files.Count} item{(_files.Count > 1 ? "s" : "")} for deletion.";

            // build preview items
            ThumbList.ItemsSource = files.Select(f =>
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                bool isImage = PhotoPrismService.ImageExts.Contains(ext);
                // fetch thumbnail into memory for preview
                var data = _svc.DownloadToMemory(f);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(data);
                bmp.EndInit();
                bmp.Freeze();
                return new
                {
                    Thumb = bmp,
                    PlayCommand = new RelayCommand(_ =>
                    {
                        if (!isImage)
                        {
                            // open external player
                            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ext);
                            File.WriteAllBytes(tmp, data);
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tmp)
                            {
                                UseShellExecute = true
                            });
                        }
                    })
                };
            }).ToList();
        }

        private async void OK_Click(object sender, RoutedEventArgs e)
        {
            // optionally download all
            if (DownloadBefore.IsChecked == true)
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Select download folder" };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    foreach (var f in _files)
                    {
                        await Task.Run(() =>
                        {
                            var data = _svc.DownloadToMemory(f);
                            File.WriteAllBytes(Path.Combine(dlg.SelectedPath, Path.GetFileName(f)), data);
                        });
                    }
                }
            }

            OK.IsEnabled = Cancel.IsEnabled = false;
            OK.Content = "Deleting...";
            var failed = await Task.Run(() => _svc.DeleteFiles(_files));
            int succ = _files.Count - failed.Count;
            string msg = failed.Count == 0
                ? $"Deleted {succ} item(s)."
                : $"Deleted {succ}, failed {failed.Count}:\n" +
                  string.Join("\n", failed.Select(Path.GetFileName));

            Lbl.Visibility = ThumbList.Visibility = DownloadBefore.Visibility = Cancel.Visibility = OK.Visibility = Visibility.Collapsed;
            ResultText.Text = msg;
            ResultText.Visibility = CloseBtn.Visibility = Visibility.Visible;
            _didDelete = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = _didDelete;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (!_didDelete) DialogResult = false;
            base.OnClosed(e);
        }
    }

    // simple ICommand impl for PlayCommand
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _exec;
        public RelayCommand(Action<object?> exec) => _exec = exec;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _exec(p);
        public event EventHandler? CanExecuteChanged;
    }
}
