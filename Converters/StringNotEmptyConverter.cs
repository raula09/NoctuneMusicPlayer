using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace MusicPlayerApp.Converters;

public class StringNotEmptyConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasValue = value is string s && !string.IsNullOrWhiteSpace(s);
        return Invert ? !hasValue : hasValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
