using Microsoft.UI.Xaml.Navigation;

namespace FolderOrganizer.App
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Exposes the main window so pages can obtain the HWND for WinRT interop.</summary>
        public MainWindow? MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var cmdArgs = Environment.GetCommandLineArgs();
            MainWindow = new MainWindow(cmdArgs);
            MainWindow.Activate();
        }
    }
}
