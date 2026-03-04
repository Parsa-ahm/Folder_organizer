using System.Diagnostics;
using FolderOrganizer.Core;
using FolderOrganizer.Core.Storage;
using FolderOrganizer.Core.Tags;

namespace FolderOrganizer.App.Views
{
    /// <summary>
    /// Settings page: Danger Zone actions, Explorer column toggle, and version info.
    /// </summary>
    public partial class SettingsPage : Page
    {
        private readonly AdsStorage _ads = new();
        private readonly TouchLog _touchLog;
        private readonly TagRegistry _tagRegistry;

        public SettingsPage()
        {
            this.InitializeComponent();
            _touchLog = new TouchLog(AppDataPaths.RootDir);
            _tagRegistry = new TagRegistry(AppDataPaths.RootDir);
        }

        // ----------------------------------------------------------------
        // Clear All Customizations
        // ----------------------------------------------------------------

        private async void ClearAll(object sender, RoutedEventArgs e)
        {
            var confirm = await ConfirmDialogAsync(
                "Clear All Customizations",
                "This will remove all color, icon, and tag metadata from every file and folder " +
                "tracked by Folder Organizer. This cannot be undone. Continue?");

            if (!confirm) return;

            ClearAllBtn.IsEnabled = false;
            try
            {
                var paths = _touchLog.GetAllTouchedPaths().ToList();
                int cleared = 0;
                foreach (var path in paths)
                {
                    try
                    {
                        _ads.ClearMetadata(path);
                        cleared++;
                    }
                    catch { /* swallow — file may have been deleted or moved */ }
                }
                _touchLog.Clear();

                await ShowInfoDialogAsync(
                    "Done",
                    $"Cleared metadata from {cleared} of {paths.Count} recorded path(s).");
            }
            catch (Exception ex)
            {
                await ShowInfoDialogAsync("Error", ex.Message);
            }
            finally
            {
                ClearAllBtn.IsEnabled = true;
            }
        }

        // ----------------------------------------------------------------
        // Reset Tag Registry
        // ----------------------------------------------------------------

        private async void ResetTags(object sender, RoutedEventArgs e)
        {
            var confirm = await ConfirmDialogAsync(
                "Reset Tag Registry",
                "All tag definitions will be deleted from tags.json. Tags embedded in file metadata " +
                "will not be affected. Continue?");

            if (!confirm) return;

            ResetTagsBtn.IsEnabled = false;
            try
            {
                _tagRegistry.Clear();
                await ShowInfoDialogAsync("Done", "Tag registry has been reset.");
            }
            catch (Exception ex)
            {
                await ShowInfoDialogAsync("Error", ex.Message);
            }
            finally
            {
                ResetTagsBtn.IsEnabled = true;
            }
        }

        // ----------------------------------------------------------------
        // Unregister Context Menu Shell Extension
        // ----------------------------------------------------------------

        private async void UnregisterShell(object sender, RoutedEventArgs e)
        {
            var confirm = await ConfirmDialogAsync(
                "Unregister Context Menu",
                "This will run 'regsvr32 /u FolderOrganizer.Shell.dll' as an administrator " +
                "to remove the right-click context menu. Continue?");

            if (!confirm) return;

            UnregisterShellBtn.IsEnabled = false;
            try
            {
                // Locate the DLL next to the running executable
                var exeDir = AppContext.BaseDirectory;
                var dllPath = System.IO.Path.Combine(exeDir, "FolderOrganizer.Shell.dll");

                var psi = new ProcessStartInfo
                {
                    FileName = "regsvr32",
                    Arguments = $"/u \"{dllPath}\"",
                    Verb = "runas",         // Request elevation
                    UseShellExecute = true
                };

                using var proc = Process.Start(psi);
                if (proc is not null)
                {
                    await Task.Run(() => proc.WaitForExit());
                    if (proc.ExitCode == 0)
                        await ShowInfoDialogAsync("Done", "Context menu extension unregistered successfully.");
                    else
                        await ShowInfoDialogAsync("Warning",
                            $"regsvr32 exited with code {proc.ExitCode}. " +
                            "The extension may not have been fully unregistered.");
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User cancelled the UAC prompt
                await ShowInfoDialogAsync("Cancelled", "Unregistration was cancelled.");
            }
            catch (Exception ex)
            {
                await ShowInfoDialogAsync("Error", ex.Message);
            }
            finally
            {
                UnregisterShellBtn.IsEnabled = true;
            }
        }

        // ----------------------------------------------------------------
        // Tags Column Toggle
        // ----------------------------------------------------------------

        private void TagsColumnToggled(object sender, RoutedEventArgs e)
        {
            // Placeholder: future implementation will write a registry key to
            // enable/disable the Windows Property Handler that surfaces the Tags column.
            // For now the setting is stored in memory only.
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private async Task<bool> ConfirmDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "Yes, continue",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
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
