using System.Threading.Tasks;
using System.Windows;

namespace PhotoPrismCleanup
{
    public partial class ImportPreviewWindow : Window
    {
        private readonly string[] _locals;
        private readonly PhotoPrismService _service;

        public ImportPreviewWindow(string[] localPaths, PhotoPrismService service)
        {
            InitializeComponent();
            _locals = localPaths;
            _service = service;
            foreach (var p in _locals)
                FileList.Items.Add(System.IO.Path.GetFileName(p));
        }

        private async void ImportOK_Click(object sender, RoutedEventArgs e)
        {
            ImportOK.IsEnabled = ImportCancel.IsEnabled = false;
            ImportProgressBar.Visibility = Visibility.Visible;

            int total = _locals.Length;
            for (int i = 0; i < total; i++)
            {
                await Task.Run(() => _service.ImportFiles(new[] { _locals[i] }));
                ImportProgressBar.Value = (double)(i + 1) / total;
            }

            MessageBox.Show("Import complete.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void ImportCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
