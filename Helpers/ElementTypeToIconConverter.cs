using FluentIcons.Common;
using PlistExplorer.Models;
using System.Globalization;
using System.Windows.Data;

namespace PlistExplorer.Helpers;

public class ElementTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PlistElementType elementType)
        {
            return elementType switch
            {
                PlistElementType.String => Symbol.Text,
                PlistElementType.Number => Symbol.NumberSymbol,
                PlistElementType.UID => Symbol.Key,
                PlistElementType.Boolean => Symbol.ToggleRight,
                PlistElementType.Date => Symbol.Calendar,
                PlistElementType.Data => Symbol.Code,
                PlistElementType.Array => Symbol.List,
                PlistElementType.Dictionary => Symbol.Folder,
                _ => Symbol.Document
            };
        }

        return Symbol.Document;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
