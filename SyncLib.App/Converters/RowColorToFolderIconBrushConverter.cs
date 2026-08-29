using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace SyncLib.App.Converters;

public class RowColorToFolderIconBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string colorStr = value as string ?? string.Empty;

        // Se a linha não for transparente, consideramos como "selecionada" ou "colorida".
        // O usuário disse: "quando a linha está selecionado em verde deve ficar branco."
        // A linha verde gerada na Grid (quando completado com sucesso ou algo parecido) seria diferente de Transparent.
        // Vamos retornar branco caso tenha alguma cor (vermelho, amarelo, verde) para destaque, ou apenas se for verde.
        if (!string.IsNullOrWhiteSpace(colorStr) && !colorStr.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
        {
            // Fica branco quando tem cor na linha
            return new SolidColorBrush(Microsoft.UI.Colors.White);
        }

        // Caso contrário, fica verde sólido
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94)); // #22C55E
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
