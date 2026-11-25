using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DraGon.App.ViewModels;
using DraGon.Core.Models;

namespace DraGon.App.Converters;

public sealed class NotificationToneToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var tone = value is NotificationTone t ? t : NotificationTone.Info;
        var hex = tone switch
        {
            NotificationTone.Success => "#10B981",
            NotificationTone.Warning => "#D97706",
            NotificationTone.Error => "#DC2626",
            _ => "#60A5FA"
        };
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ServiceStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is ServiceStatus s ? s : ServiceStatus.Stopped;
        var hex = status switch
        {
            ServiceStatus.Running => "#10B981",
            ServiceStatus.Starting => "#60A5FA",
            ServiceStatus.Stopping => "#D97706",
            ServiceStatus.MissingRuntime => "#D97706",
            ServiceStatus.PortConflict => "#DC2626",
            ServiceStatus.Error => "#DC2626",
            _ => "#71717A"
        };
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DomainStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is DomainSyncStatus d ? d : DomainSyncStatus.NotSynced;
        var hex = status switch
        {
            DomainSyncStatus.Synced => "#10B981",
            DomainSyncStatus.NotSynced => "#D97706",
            DomainSyncStatus.ExternalConflict => "#DC2626",
            DomainSyncStatus.InvalidDomain => "#DC2626",
            _ => "#71717A"
        };
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        return flag ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StringEqualToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value?.ToString() ?? string.Empty;
        var target = parameter?.ToString() ?? string.Empty;
        return string.Equals(current, target, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StringEqualToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value?.ToString() ?? string.Empty;
        var target = parameter?.ToString() ?? string.Empty;
        return string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null)
        {
            return parameter.ToString()!;
        }
        return Binding.DoNothing;
    }
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}
