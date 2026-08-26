using System.Globalization;
using PcMarket.Contracts.Orders;
using PcMarket.Mobile.Core;
using PcMarket.Mobile.Services;

namespace PcMarket.Mobile;

/// <summary>Formats a decimal as UZS. Used inside item templates, where the bound object is a DTO record
/// that cannot carry display strings of its own.</summary>
public sealed class MoneyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is decimal amount ? Format.Money(amount) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTimeOffset date ? Format.Date(date) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class OrderStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is OrderStatus status ? Format.Status(status) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag && !flag;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag && !flag;
}

/// <summary>True when a string has content — for showing a label only once it says something.</summary>
public sealed class HasTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Renders a variant's attribute dictionary as "Colour: Black · Size: M" for pickers and lists.</summary>
public sealed class AttributesConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IReadOnlyDictionary<string, string> attributes && attributes.Count > 0
            ? string.Join(" · ", attributes.Select(pair => $"{pair.Key}: {pair.Value}"))
            : "Standard";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Turns a URL into a cached <see cref="UriImageSource"/>, resolving a storefront-relative path
/// against the media root on the way. Binding a bare string to <c>Image.Source</c> would also produce a
/// URI source, but only for a path that is already absolute and only on MAUI's own one-day cache; both of
/// those are decisions this app needs to make for itself.</summary>
public sealed class RemoteImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Artwork.Source(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Turns a category slug into its tile artwork. Separate from <see cref="RemoteImageConverter"/>
/// because the catalogue contract carries no image for a category: the file is resolved from the slug,
/// through the same stand-in map the storefront uses, rather than read off the DTO.</summary>
public sealed class CategoryArtConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string slug && !string.IsNullOrWhiteSpace(slug)
            ? Artwork.Source(Artwork.Category(slug))
            : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when a list item is the one currently selected. Takes the item and the selection as
/// two values, because a pill inside a template has to compare itself against a property on the view
/// model and a <see cref="IValueConverter"/> can only see one of the two.</summary>
/// <remarks>The alternative is a wrapper type per list carrying an <c>IsSelected</c> flag, which puts
/// selection state into the view models this slice is not supposed to change.</remarks>
public sealed class SelectionConverter : IMultiValueConverter
{
    public object Convert(object?[]? values, Type targetType, object? parameter, CultureInfo culture) =>
        values is { Length: 2 } && values[0] is not null && Equals(values[0], values[1]);

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the bound value is null. A <c>DataTrigger</c> with <c>Value="{x:Null}"</c> does
/// not fire - measured on device, the catalog's "All" pill stayed unlit with no category selected -
/// so "nothing is selected" is tested through a converter instead.</summary>
public sealed class IsNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
