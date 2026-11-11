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
            NotificationTone.Warning => "#F59E0B",
            NotificationTone.Error => "#EF4444",
            _ => "#38BDF8"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
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
            ServiceStatus.Running => "#10B981",       // Emerald
            ServiceStatus.Starting => "#38BDF8",      // Sky Blue
            ServiceStatus.Stopping => "#F59E0B",      // Amber
            ServiceStatus.MissingRuntime => "#F59E0B",// Amber
            ServiceStatus.PortConflict => "#EF4444",  // Red
            ServiceStatus.Error => "#EF4444",         // Red
            _ => "#64748B"                            // Slate
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
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
            DomainSyncStatus.NotSynced => "#F59E0B",
            DomainSyncStatus.ExternalConflict => "#EF4444",
            DomainSyncStatus.InvalidDomain => "#EF4444",
            _ => "#64748B"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
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
