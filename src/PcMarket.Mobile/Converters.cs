using System.Globalization;
using PcMarket.Contracts.Orders;
using PcMarket.Mobile.Core;

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
