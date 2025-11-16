using System;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MusicPlayerApp.Converters
{
    public class BooleanToBrushConverter : IValueConverter
    {
        public static BooleanToBrushConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool fav = value is bool b && b;
            return new SolidColorBrush(Color.Parse(fav ? "#FF4CAF50" : "#555555"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }
}
