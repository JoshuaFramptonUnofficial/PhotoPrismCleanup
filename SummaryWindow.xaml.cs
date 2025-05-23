using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

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
            Lbl.Text = $"You selected {_files.Count} item(s) for deletion. Proceed?";
            ListBox.ItemsSource = _files;
            OK.Content = _files.Count == 1 ? "Delete Item" : $"Delete {_files.Count} Items";
        }

        private async void OK_Click(object sender, RoutedEventArgs e)
        {
            OK.IsEnabled = Cancel.IsEnabled = false;
            OK.Content = "Deleting...";
            var failed = await Task.Run(() => _svc.DeleteFiles(_files));
            int succ = _files.Count - failed.Count;
            string msg = failed.Count == 0
                ? $"Deleted {succ} item(s)."
                : $"Deleted {succ}, failed {failed.Count}:\n" +
                  string.Join("\n", failed.ConvertAll(Path.GetFileName));

            Lbl.Visibility = ListBox.Visibility = Cancel.Visibility = OK.Visibility = Visibility.Collapsed;
            ResultText.Text = msg;
            ResultText.Visibility = CloseBtn.Visibility = Visibility.Visible;
            _didDelete = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => Close();

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
}
