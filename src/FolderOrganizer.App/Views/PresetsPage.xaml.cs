using FolderOrganizer.App.ViewModels;
using Windows.Storage.Pickers;

namespace FolderOrganizer.App.Views
{
    /// <summary>
    /// Presets page: Colors, Icons, and Tags sections backed by PresetsViewModel.
    /// </summary>
    public partial class PresetsPage : Page
    {
        public PresetsViewModel ViewModel { get; } = new();

        public PresetsPage()
        {
            this.InitializeComponent();
        }

        // ----------------------------------------------------------------
        // COLORS
        // ----------------------------------------------------------------

        private async void AddColorClicked(object sender, RoutedEventArgs e)
        {
            var picker = new ColorPicker
            {
                ColorSpectrumShape = ColorSpectrumShape.Ring,
                IsAlphaEnabled = false,
                IsHexInputVisible = true
            };

            var dialog = new ContentDialog
            {
                Title = "Pick a Color",
                Content = picker,
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var c = picker.Color;
                var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                ViewModel.AddCustomColor(hex);
            }
        }

        // ----------------------------------------------------------------
        // ICONS
        // ----------------------------------------------------------------

        private async void AddIconClicked(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".ico");
            picker.FileTypeFilter.Add(".jpg");

            // Obtain the HWND for unpackaged WinRT interop
            var hwnd = GetMainWindowHwnd();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file is not null)
            {
                ViewModel.AddIconPath(file.Path);
            }
        }

        // ----------------------------------------------------------------
        // TAGS
        // ----------------------------------------------------------------

        private void AddTagClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.AddNewTag();
        }

        private void TagName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is TagEntryViewModel vm)
            {
                vm.Name = tb.Text;
                vm.SaveCommand?.Execute(null);
            }
        }

        private void TagColor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is TagEntryViewModel vm)
            {
                vm.HexColor = tb.Text;
                vm.SaveCommand?.Execute(null);
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
    }
}
