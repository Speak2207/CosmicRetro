using System.Collections.Concurrent;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace CosmicRetro.App.Views;

public sealed class LocalImageConverter : IValueConverter
{
    private readonly ConcurrentDictionary<string, Bitmap> _cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return _cache.GetOrAdd(path, static filePath => new Bitmap(filePath));
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
