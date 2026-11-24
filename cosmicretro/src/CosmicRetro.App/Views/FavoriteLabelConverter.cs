using Avalonia.Data.Converters;

namespace CosmicRetro.App.Views;

public sealed class FavoriteLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is true ? "FAVORITO" : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
