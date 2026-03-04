using FolderOrganizer.Core;
using FolderOrganizer.Core.Themes;
using Windows.Storage.Pickers;
using Windows.System;

namespace FolderOrganizer.App.Views
{
    /// <summary>
    /// Themes page: lets the user browse the VS Code Marketplace and import
    /// .vsix / .zip icon theme archives via <see cref="VsixThemeImporter"/>.
    /// </summary>
    public partial class ThemesPage : Page
    {
        private const string MarketplaceUrl =
            "https://marketplace.visualstudio.com/search?target=VSCode&category=Themes&sortBy=Installs";

        private readonly VsixThemeImporter _importer = new();

        public ThemesPage()
        {
            this.InitializeComponent();
            LoadActiveThemeLabel();
        }

        // ----------------------------------------------------------------
        // Page load
        // ----------------------------------------------------------------

        private void LoadActiveThemeLabel()
        {
            try
            {
                var themeFile = AppDataPaths.ThemeFile;
                if (System.IO.File.Exists(themeFile))
                {
                    var json = System.IO.File.ReadAllText(themeFile);
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("label", out var label))
                    {
                        ActiveThemeLabel.Text = label.GetString() ?? "Unknown theme";
                        return;
                    }
                }
            }
            catch { }
            ActiveThemeLabel.Text = "No theme applied";
        }

        // ----------------------------------------------------------------
        // Button handlers
        // ----------------------------------------------------------------

        private async void OpenMarketplace(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri(MarketplaceUrl));
        }

        private async void ImportTheme(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".vsix");
            picker.FileTypeFilter.Add(".zip");

            var hwnd = GetMainWindowHwnd();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            try
            {
                var themesDir = System.IO.Path.Combine(AppDataPaths.RootDir, "themes",
                    System.IO.Path.GetFileNameWithoutExtension(file.Name));

                var manifest = _importer.Import(file.Path, themesDir);

                // Persist active theme metadata
                var meta = new { label = manifest.Label ?? file.Name };
                var json = System.Text.Json.JsonSerializer.Serialize(meta,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(AppDataPaths.ThemeFile, json);

                ActiveThemeLabel.Text = manifest.Label ?? file.Name;

                await ShowInfoDialogAsync(
                    "Theme Imported",
                    $"Successfully imported theme: {manifest.Label ?? file.Name}");
            }
            catch (Exception ex)
            {
                await ShowInfoDialogAsync("Import Failed", ex.Message);
            }
        }

        private async void RemoveTheme(object sender, RoutedEventArgs e)
        {
            try
            {
                if (System.IO.File.Exists(AppDataPaths.ThemeFile))
                    System.IO.File.Delete(AppDataPaths.ThemeFile);
                ActiveThemeLabel.Text = "No theme applied";
            }
            catch (Exception ex)
            {
                await ShowInfoDialogAsync("Error", ex.Message);
            }
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static nint GetMainWindowHwnd()
        {
            if (Application.Current is App app && app.MainWindow is not null)
                return WinRT.Interop.WindowNative.GetWindowHandle(app.MainWindow);
            return nint.Zero;
        }

        private Task ShowInfoDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            return dialog.ShowAsync().AsTask();
        }
    }
}
