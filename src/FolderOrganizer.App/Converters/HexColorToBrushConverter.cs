using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FolderOrganizer.App.Converters
{
    /// <summary>
    /// Converts a hex color string (e.g. "#EA4335") to a SolidColorBrush for XAML binding.
    /// </summary>
    public class HexColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    var color = ParseHex(hex);
                    return new SolidColorBrush(color);
                }
                catch { }
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();

        private static Color ParseHex(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                var r = System.Convert.ToByte(hex.Substring(0, 2), 16);
                var g = System.Convert.ToByte(hex.Substring(2, 2), 16);
                var b = System.Convert.ToByte(hex.Substring(4, 2), 16);
                return Color.FromArgb(255, r, g, b);
            }
            if (hex.Length == 8)
            {
                var a = System.Convert.ToByte(hex.Substring(0, 2), 16);
                var r = System.Convert.ToByte(hex.Substring(2, 2), 16);
                var g = System.Convert.ToByte(hex.Substring(4, 2), 16);
                var b = System.Convert.ToByte(hex.Substring(6, 2), 16);
                return Color.FromArgb(a, r, g, b);
            }
            throw new FormatException($"Cannot parse hex color: #{hex}");
        }
    }
}
