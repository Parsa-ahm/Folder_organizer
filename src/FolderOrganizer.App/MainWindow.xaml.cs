using Microsoft.UI.Xaml.Navigation;

namespace FolderOrganizer.App
{
    /// <summary>
    /// The main application window hosting the NavigationView shell.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Activated += Window_Activated;
        }

        public MainWindow(string[] cmdArgs) : this()
        {
            // Parse --action and --path for shell-extension launch scenarios
            // e.g. FolderOrganizer.App.exe --action color --path "C:\Some\File.txt"
            ParseCommandLineArgs(cmdArgs);
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            // Navigate to Presets on first activation
            if (ContentFrame.Content is null)
            {
                NavView.SelectedItem = NavView.MenuItems[0];
                ContentFrame.Navigate(typeof(PresetsPage));
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var tag = (args.SelectedItem as NavigationViewItem)?.Tag?.ToString();
            var pageType = tag switch
            {
                "Presets" => typeof(PresetsPage),
                "Themes" => typeof(ThemesPage),
                "Settings" => typeof(SettingsPage),
                _ => typeof(PresetsPage)
            };
            ContentFrame.Navigate(pageType);
        }

        private void ParseCommandLineArgs(string[] args)
        {
            string? action = null;
            string? path = null;

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--action") action = args[i + 1];
                else if (args[i] == "--path") path = args[i + 1];
            }

            if (action is not null && path is not null)
            {
                // Navigate based on the action requested by the shell extension
                // Future: open specific dialogs with the file path pre-selected
                switch (action.ToLowerInvariant())
                {
                    case "color":
                    case "icon":
                    case "tag":
                        NavView.SelectedItem = NavView.MenuItems[0]; // Presets
                        ContentFrame.Navigate(typeof(PresetsPage), path);
                        break;
                    case "theme":
                        NavView.SelectedItem = NavView.MenuItems[1]; // Themes
                        ContentFrame.Navigate(typeof(ThemesPage));
                        break;
                }
            }
        }
    }
}
