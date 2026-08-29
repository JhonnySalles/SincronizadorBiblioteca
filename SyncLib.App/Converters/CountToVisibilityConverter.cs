using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace SyncLib.App.Converters;

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        int count = value is int i ? i : 0;
        bool invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);

        if (invert)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        return count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
