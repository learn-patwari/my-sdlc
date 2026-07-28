using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SprintForge.Domain.Approvals;
using SprintForge.Domain.Audit;
using System;
using System.Globalization;

namespace SprintForge.Avalonia.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hex && !string.IsNullOrEmpty(hex) && hex != "Transparent"
            ? new SolidColorBrush(Color.Parse(hex))
            : Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class LeftToThicknessConverter : IValueConverter
{
    public static readonly LeftToThicknessConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? new Thickness(d, 0, 0, 0) : new Thickness(0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibleConverter : IValueConverter
{
    public static readonly NullToVisibleConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NotNullToVisibleConverter : IValueConverter
{
    public static readonly NotNullToVisibleConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class CollapseIconConverter : IValueConverter
{
    public static readonly CollapseIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "◀" : "▶";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SrsStatusBgConverter : IValueConverter
{
    public static readonly SrsStatusBgConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new SolidColorBrush(Color.Parse(value is true ? "#052E16" : "#451A03"));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SrsStatusFgConverter : IValueConverter
{
    public static readonly SrsStatusFgConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new SolidColorBrush(Color.Parse(value is true ? "#22C55E" : "#F59E0B"));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SrsStatusTextConverter : IValueConverter
{
    public static readonly SrsStatusTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Ready to Submit" : "Pending Generation";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ApprovalStatusBgConverter : IValueConverter
{
    public static readonly ApprovalStatusBgConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value is ApprovalStatus s ? s switch
        {
            ApprovalStatus.Approved  or ApprovalStatus.Completed => "#052E16",
            ApprovalStatus.Rejected  or ApprovalStatus.Failed    => "#2D0707",
            ApprovalStatus.Executing                             => "#0F2040",
            _                                                    => "#451A03"
        } : "#451A03";
        return new SolidColorBrush(Color.Parse(hex));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ApprovalStatusFgConverter : IValueConverter
{
    public static readonly ApprovalStatusFgConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value is ApprovalStatus s ? s switch
        {
            ApprovalStatus.Approved  or ApprovalStatus.Completed => "#22C55E",
            ApprovalStatus.Rejected  or ApprovalStatus.Failed    => "#EF4444",
            ApprovalStatus.Executing                             => "#3B82F6",
            _                                                    => "#F59E0B"
        } : "#F59E0B";
        return new SolidColorBrush(Color.Parse(hex));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class AuditStatusBgConverter : IValueConverter
{
    public static readonly AuditStatusBgConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value is AuditStatus s ? s switch
        {
            AuditStatus.Completed or AuditStatus.Approved => "#052E16",
            AuditStatus.Failed    or AuditStatus.Rejected  => "#2D0707",
            AuditStatus.Started                            => "#0F2040",
            _                                              => "#1A2235"
        } : "#1A2235";
        return new SolidColorBrush(Color.Parse(hex));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class AuditStatusFgConverter : IValueConverter
{
    public static readonly AuditStatusFgConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value is AuditStatus s ? s switch
        {
            AuditStatus.Completed or AuditStatus.Approved => "#22C55E",
            AuditStatus.Failed    or AuditStatus.Rejected  => "#EF4444",
            AuditStatus.Started                            => "#3B82F6",
            _                                              => "#94A3B8"
        } : "#94A3B8";
        return new SolidColorBrush(Color.Parse(hex));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToFontWeightConverter : IValueConverter
{
    public static readonly BoolToFontWeightConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontWeight.SemiBold : FontWeight.Normal;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToSrsPickerLabelConverter : IValueConverter
{
    public static readonly BoolToSrsPickerLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "− Add from SRS ▴" : "+ Add from SRS ▾";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
