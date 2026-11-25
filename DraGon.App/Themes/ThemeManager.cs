using System.Windows;
using System.Windows.Media;
using DraGon.Core.Models;

namespace DraGon.App.Themes;

public static class ThemeManager
{
    public static void ApplyTheme(AppTheme theme)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        if (theme == AppTheme.Light)
        {
            SetBrush(resources, "WindowBgBrush", "#F6F6F7");
            SetBrush(resources, "SidebarBgBrush", "#FFFFFF");
            SetBrush(resources, "CardBgBrush", "#FFFFFF");
            SetBrush(resources, "CardAltBgBrush", "#F1F1F4");
            SetBrush(resources, "InputBgBrush", "#FFFFFF");
            SetBrush(resources, "BorderSubtleBrush", "#E4E4E7");
            SetBrush(resources, "TextPrimaryBrush", "#18181B");
            SetBrush(resources, "TextSecondaryBrush", "#52525B");
            SetBrush(resources, "TextMutedBrush", "#71717A");
            SetBrush(resources, "AccentPrimaryBrush", "#EA580C");
            SetBrush(resources, "AccentHoverBrush", "#C2410C");
            SetBrush(resources, "AccentSoftBgBrush", "#FFF3EC");
            SetBrush(resources, "SecondaryBtnBgBrush", "#F4F4F5");
            SetBrush(resources, "SecondaryBtnHoverBrush", "#E4E4E7");
            SetBrush(resources, "TerminalBgBrush", "#18181B");
            SetBrush(resources, "TerminalTextBrush", "#E4E4E7");
        }
        else
        {
            SetBrush(resources, "WindowBgBrush", "#0E0E10");
            SetBrush(resources, "SidebarBgBrush", "#141417");
            SetBrush(resources, "CardBgBrush", "#19191D");
            SetBrush(resources, "CardAltBgBrush", "#222227");
            SetBrush(resources, "InputBgBrush", "#111114");
            SetBrush(resources, "BorderSubtleBrush", "#2A2A30");
            SetBrush(resources, "TextPrimaryBrush", "#F4F4F5");
            SetBrush(resources, "TextSecondaryBrush", "#A1A1AA");
            SetBrush(resources, "TextMutedBrush", "#71717A");
            SetBrush(resources, "AccentPrimaryBrush", "#EA580C");
            SetBrush(resources, "AccentHoverBrush", "#F97316");
            SetBrush(resources, "AccentSoftBgBrush", "#261812");
            SetBrush(resources, "SecondaryBtnBgBrush", "#222227");
            SetBrush(resources, "SecondaryBtnHoverBrush", "#2C2C33");
            SetBrush(resources, "TerminalBgBrush", "#09090B");
            SetBrush(resources, "TerminalTextBrush", "#D4D4D8");
        }

        SetBrush(resources, "StatusSuccessBrush", "#10B981");
        SetBrush(resources, "StatusSuccessSoftBrush", theme == AppTheme.Light ? "#ECFDF5" : "#0E241C");
        SetBrush(resources, "StatusWarningBrush", "#D97706");
        SetBrush(resources, "StatusWarningSoftBrush", theme == AppTheme.Light ? "#FFFBEB" : "#261C0E");
        SetBrush(resources, "StatusDangerBrush", "#DC2626");
        SetBrush(resources, "StatusDangerSoftBrush", theme == AppTheme.Light ? "#FEF2F2" : "#291416");
        SetBrush(resources, "StatusInfoBrush", theme == AppTheme.Light ? "#2563EB" : "#60A5FA");
        SetBrush(resources, "StatusInfoSoftBrush", theme == AppTheme.Light ? "#EFF6FF" : "#121E33");
    }

    private static void SetBrush(ResourceDictionary dict, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        dict[key] = brush;
    }
}
