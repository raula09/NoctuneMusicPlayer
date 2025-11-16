using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace MusicPlayerApp
{
    public class SliderValueToWidthConverter : IValueConverter
    {
        public static readonly SliderValueToWidthConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return 0;

            if (value is double v && parameter is Slider slider && slider.Maximum > 0)
                return (v / slider.Maximum) * slider.Bounds.Width;

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }
}
