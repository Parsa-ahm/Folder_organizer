using System.Collections.ObjectModel;
using System.Windows.Input;
using FolderOrganizer.Core.Icons;
using FolderOrganizer.Core.Models;
using FolderOrganizer.Core.Tags;

namespace FolderOrganizer.App.ViewModels
{
    /// <summary>
    /// Backing view-model for PresetsPage.
    /// Owns the Colors, Icons, and Tags collections.
    /// </summary>
    public class PresetsViewModel
    {
        private readonly TagRegistry _tagRegistry;

        public ObservableCollection<ColorPresetViewModel> Colors { get; } = new();
        public ObservableCollection<string> CustomIconPaths { get; } = new();
        public ObservableCollection<TagEntryViewModel> Tags { get; } = new();

        public PresetsViewModel()
        {
            _tagRegistry = new TagRegistry(Core.AppDataPaths.RootDir);
            LoadColors();
            LoadIcons();
            LoadTags();
        }

        // ----------------------------------------------------------------
        // Colors
        // ----------------------------------------------------------------

        private void LoadColors()
        {
            foreach (var (name, hex) in ChromePalette.Colors)
            {
                Colors.Add(new ColorPresetViewModel(name, hex, isCustom: false));
            }
        }

        public void AddCustomColor(string hexColor)
        {
            var vm = new ColorPresetViewModel("Custom", hexColor, isCustom: true);
            vm.DeleteCommand = new RelayCommand(() => Colors.Remove(vm));
            Colors.Add(vm);
        }

        // ----------------------------------------------------------------
        // Icons
        // ----------------------------------------------------------------

        private void LoadIcons()
        {
            var iconsDir = Core.AppDataPaths.CustomIconsDir;
            if (!System.IO.Directory.Exists(iconsDir)) return;

            foreach (var file in System.IO.Directory.EnumerateFiles(iconsDir, "*.*")
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)))
            {
                CustomIconPaths.Add(file);
            }
        }

        public void AddIconPath(string sourcePath)
        {
            var dest = System.IO.Path.Combine(
                Core.AppDataPaths.CustomIconsDir,
                System.IO.Path.GetFileName(sourcePath));
            System.IO.File.Copy(sourcePath, dest, overwrite: true);
            if (!CustomIconPaths.Contains(dest))
                CustomIconPaths.Add(dest);
        }

        public void RemoveIcon(string path)
        {
            try { System.IO.File.Delete(path); } catch { }
            CustomIconPaths.Remove(path);
        }

        // ----------------------------------------------------------------
        // Tags
        // ----------------------------------------------------------------

        private void LoadTags()
        {
            foreach (var tag in _tagRegistry.GetAll())
            {
                AddTagViewModel(tag.Name, tag.Color);
            }
        }

        public void AddNewTag()
        {
            var vm = AddTagViewModel("New Tag", "#5F6368");
            _tagRegistry.AddOrUpdate(new TagEntry { Name = vm.Name, Color = vm.HexColor });
        }

        private TagEntryViewModel AddTagViewModel(string name, string hexColor)
        {
            var vm = new TagEntryViewModel(name, hexColor);
            vm.DeleteCommand = new RelayCommand(() =>
            {
                _tagRegistry.Delete(vm.Name);
                Tags.Remove(vm);
            });
            vm.SaveCommand = new RelayCommand(() =>
            {
                _tagRegistry.AddOrUpdate(new TagEntry { Name = vm.Name, Color = vm.HexColor });
            });
            Tags.Add(vm);
            return vm;
        }
    }

    // ----------------------------------------------------------------
    // Simple relay command (no heavy MVVM framework needed)
    // ----------------------------------------------------------------

    internal sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged;
    }
}
