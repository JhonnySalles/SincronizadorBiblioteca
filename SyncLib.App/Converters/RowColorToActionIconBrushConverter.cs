using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace SyncLib.App.Converters;

public class RowColorToActionIconBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string colorStr = value as string ?? string.Empty;

        // Se for linha transparente (normal), exibe o vermelho padrão #EF4444
        if (string.IsNullOrWhiteSpace(colorStr) || colorStr.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68)); // #EF4444
        }

        // Se a linha tiver cor (amarelo, vermelho, verde, etc.), exibe na cor fixa preta para dar destaque
        return new SolidColorBrush(Microsoft.UI.Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
