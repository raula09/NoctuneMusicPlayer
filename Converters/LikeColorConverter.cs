using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace MusicPlayerApp.Converters;

public class LikeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isLiked && isLiked)
        {
            return new SolidColorBrush(Color.Parse("#1DB954")); // Spotify green when liked
        }
        
        return new SolidColorBrush(Color.Parse("#6A6A6A")); // Gray when not liked
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}