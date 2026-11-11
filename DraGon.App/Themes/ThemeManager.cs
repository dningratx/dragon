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
            SetBrush(resources, "WindowBgBrush", "#F4F6FB");
            SetBrush(resources, "SidebarBgBrush", "#FFFFFF");
            SetBrush(resources, "CardBgBrush", "#FFFFFF");
            SetBrush(resources, "CardAltBgBrush", "#EEF2F9");
            SetBrush(resources, "InputBgBrush", "#F8FAFC");
            SetBrush(resources, "BorderSubtleBrush", "#D8E0EE");
            SetBrush(resources, "TextPrimaryBrush", "#0F172A");
            SetBrush(resources, "TextSecondaryBrush", "#475569");
            SetBrush(resources, "TextMutedBrush", "#64748B");
            SetBrush(resources, "AccentPrimaryBrush", "#FF5722");
            SetBrush(resources, "AccentHoverBrush", "#E64A19");
            SetBrush(resources, "AccentSoftBgBrush", "#FFF0EB");
            SetBrush(resources, "SecondaryBtnBgBrush", "#E2E8F0");
            SetBrush(resources, "SecondaryBtnHoverBrush", "#CBD5E1");
            SetBrush(resources, "TerminalBgBrush", "#0F172A");
            SetBrush(resources, "TerminalTextBrush", "#E2E8F0");
        }
        else
        {
            // Default Dark Theme (Developer Dark Slate + Dragon Ember Orange/Cyan accents)
            SetBrush(resources, "WindowBgBrush", "#0B0F17");
            SetBrush(resources, "SidebarBgBrush", "#111827");
            SetBrush(resources, "CardBgBrush", "#161F30");
            SetBrush(resources, "CardAltBgBrush", "#1E293B");
            SetBrush(resources, "InputBgBrush", "#0F172A");
            SetBrush(resources, "BorderSubtleBrush", "#283548");
            SetBrush(resources, "TextPrimaryBrush", "#F8FAFC");
            SetBrush(resources, "TextSecondaryBrush", "#94A3B8");
            SetBrush(resources, "TextMutedBrush", "#64748B");
            SetBrush(resources, "AccentPrimaryBrush", "#FF6B35");
            SetBrush(resources, "AccentHoverBrush", "#FF8255");
            SetBrush(resources, "AccentSoftBgBrush", "#2A1B1E");
            SetBrush(resources, "SecondaryBtnBgBrush", "#243247");
            SetBrush(resources, "SecondaryBtnHoverBrush", "#334155");
            SetBrush(resources, "TerminalBgBrush", "#070A10");
            SetBrush(resources, "TerminalTextBrush", "#CBD5E1");
        }

        // Semantic Status Colors (consistent and high-contrast on both themes)
        SetBrush(resources, "StatusSuccessBrush", "#10B981");
        SetBrush(resources, "StatusSuccessSoftBrush", theme == AppTheme.Light ? "#D1FAE5" : "#0F2922");
        SetBrush(resources, "StatusWarningBrush", "#F59E0B");
        SetBrush(resources, "StatusWarningSoftBrush", theme == AppTheme.Light ? "#FEF3C7" : "#2D2112");
        SetBrush(resources, "StatusDangerBrush", "#EF4444");
        SetBrush(resources, "StatusDangerSoftBrush", theme == AppTheme.Light ? "#FEE2E2" : "#2D161B");
        SetBrush(resources, "StatusInfoBrush", "#38BDF8");
        SetBrush(resources, "StatusInfoSoftBrush", theme == AppTheme.Light ? "#E0F2FE" : "#112438");
    }

    private static void SetBrush(ResourceDictionary dict, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        dict[key] = brush;
    }
}
