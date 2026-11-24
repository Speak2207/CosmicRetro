using Avalonia.Data.Converters;
using CosmicRetro.Core.Platforms;

namespace CosmicRetro.App.Views;

public sealed class PlatformNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is string platformId
            ? PlatformCatalog.GetRequired(platformId).Name
            : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
