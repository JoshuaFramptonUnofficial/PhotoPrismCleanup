using System;
using System.Windows;

namespace PhotoPrismCleanup
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var cfg = ConfigService.Load();
            ApplyTheme(cfg.Theme);
        }

        public static void ApplyTheme(ThemeMode theme)
        {
            Current.Resources.MergedDictionaries.Clear();
            var uri = new Uri($"Themes/{(theme == ThemeMode.Dark ? "Dark" : "Light")}Theme.xaml", UriKind.Relative);
            var dict = new ResourceDictionary { Source = uri };
            Current.Resources.MergedDictionaries.Add(dict);
        }
    }
}
