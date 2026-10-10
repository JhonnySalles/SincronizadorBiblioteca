using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace SyncLib.App.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool val = value is bool b && b;
        if (Invert) val = !val;
        return val ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility v)
        {
            bool res = v == Visibility.Visible;
            return Invert ? !res : res;
        }
        return false;
    }
}
