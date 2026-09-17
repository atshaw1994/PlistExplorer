using System.Globalization;
using System.Windows.Data;

namespace PlistExplorer.Helpers;

public class StringToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue)
        {
            return strValue.Equals("true", StringComparison.OrdinalIgnoreCase) || strValue == "1";
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "true" : "false";
        }
        return "false";
    }
}
