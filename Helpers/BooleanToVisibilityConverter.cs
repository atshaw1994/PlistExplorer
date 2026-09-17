using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PlistExplorer.Helpers;

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            bool isInverse = string.Equals(parameter?.ToString(), "Inverse", StringComparison.OrdinalIgnoreCase);

            if (isInverse)
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool isInverse = string.Equals(parameter?.ToString(), "Inverse", StringComparison.OrdinalIgnoreCase);
            bool result = visibility == Visibility.Visible;

            return isInverse ? !result : result;
        }

        return false;
    }
}
