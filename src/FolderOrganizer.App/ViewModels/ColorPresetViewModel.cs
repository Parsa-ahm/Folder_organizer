using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FolderOrganizer.App.ViewModels
{
    /// <summary>
    /// Represents a single color swatch in the Presets page Colors section.
    /// </summary>
    public class ColorPresetViewModel : INotifyPropertyChanged
    {
        private string _hexColor;
        private bool _isCustom;

        public string HexColor
        {
            get => _hexColor;
            set { _hexColor = value; OnPropertyChanged(); }
        }

        public string Name { get; set; }

        public bool IsCustom
        {
            get => _isCustom;
            set { _isCustom = value; OnPropertyChanged(); }
        }

        public ICommand? DeleteCommand { get; set; }

        public ColorPresetViewModel(string name, string hexColor, bool isCustom = false)
        {
            Name = name;
            _hexColor = hexColor;
            _isCustom = isCustom;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
