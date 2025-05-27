using System;
using System.Windows;

namespace PhotoPrismCleanup
{
    public partial class VideoPreviewWindow : Window
    {
        public VideoPreviewWindow(string path)
        {
            InitializeComponent();
            VideoPlayer.Source = new Uri(path);
            VideoPlayer.Play();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Stop();
            Close();
        }
    }
}
