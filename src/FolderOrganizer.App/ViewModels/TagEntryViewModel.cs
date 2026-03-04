using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FolderOrganizer.App.ViewModels
{
    /// <summary>
    /// Wraps a TagEntry for display in the Presets page Tags section.
    /// </summary>
    public class TagEntryViewModel : INotifyPropertyChanged
    {
        private string _name;
        private string _hexColor;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string HexColor
        {
            get => _hexColor;
            set { _hexColor = value; OnPropertyChanged(); }
        }

        public ICommand? DeleteCommand { get; set; }
        public ICommand? SaveCommand { get; set; }

        public TagEntryViewModel(string name, string hexColor)
        {
            _name = name;
            _hexColor = hexColor;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
