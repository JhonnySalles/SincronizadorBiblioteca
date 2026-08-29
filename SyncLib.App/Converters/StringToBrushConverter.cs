using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Globalization;

namespace SyncLib.App.Converters;

public class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string colorStr && !string.IsNullOrWhiteSpace(colorStr))
        {
            if (colorStr.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Colors.Transparent);
            }

            try
            {
                colorStr = colorStr.TrimStart('#');
                if (colorStr.Length == 8) // ARGB
                {
                    byte a = byte.Parse(colorStr.Substring(0, 2), NumberStyles.HexNumber);
                    byte r = byte.Parse(colorStr.Substring(2, 2), NumberStyles.HexNumber);
                    byte g = byte.Parse(colorStr.Substring(4, 2), NumberStyles.HexNumber);
                    byte b = byte.Parse(colorStr.Substring(6, 2), NumberStyles.HexNumber);
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
                }
                else if (colorStr.Length == 6) // RGB
                {
                    byte r = byte.Parse(colorStr.Substring(0, 2), NumberStyles.HexNumber);
                    byte g = byte.Parse(colorStr.Substring(2, 2), NumberStyles.HexNumber);
                    byte b = byte.Parse(colorStr.Substring(4, 2), NumberStyles.HexNumber);
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
                }
            }
            catch
            {
                // Fallback se falhar ao analisar
            }
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
