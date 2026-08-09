using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ROMVault.Avalonia.Converters;

/// <summary>
/// Base class for converters that intentionally support only source-to-target conversion.
/// </summary>
public abstract class OneWayValueConverter : IValueConverter
{
    public abstract object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture);

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) => BindingOperations.DoNothing;
}
