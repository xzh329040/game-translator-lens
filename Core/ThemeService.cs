using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;

namespace GameTranslatorLens.Core;

public static class ThemeService
{
    public const string Dark = "Dark";
    public const string Light = "Light";

    public static string Normalize(string mode) =>
        string.Equals(mode, Light, StringComparison.OrdinalIgnoreCase) ? Light : Dark;

    public static void Apply(string mode)
    {
        ThemePalette palette = Normalize(mode) == Light
            ? ThemePalette.Light
            : ThemePalette.Dark;
        ResourceDictionary resources = WpfApplication.Current.Resources;
        SetColor(resources, "ColorWindow", palette.Window);
        SetColor(resources, "ColorSurface", palette.Surface);
        SetColor(resources, "ColorSurfaceAlt", palette.SurfaceAlt);
        SetColor(resources, "ColorSurfaceHover", palette.SurfaceHover);
        SetColor(resources, "ColorBorder", palette.Border);
        SetColor(resources, "ColorBorderSoft", palette.BorderSoft);
        SetColor(resources, "ColorPrimary", palette.Primary);
        SetColor(resources, "ColorPrimaryHover", palette.PrimaryHover);
        SetColor(resources, "ColorCyan", palette.Cyan);
        SetColor(resources, "ColorSuccess", palette.Success);
        SetColor(resources, "ColorWarning", palette.Warning);
        SetColor(resources, "ColorDanger", palette.Danger);
        SetColor(resources, "ColorText", palette.Text);
        SetColor(resources, "ColorTextSecondary", palette.TextSecondary);
        SetColor(resources, "ColorTextMuted", palette.TextMuted);
        SetColor(resources, "ColorStartButton", palette.StartButton);
        SetColor(resources, "ColorStartButtonHover", palette.StartButtonHover);
        SetColor(resources, "ColorGhostButton", palette.GhostButton);
        SetColor(resources, "ColorGhostButtonHover", palette.GhostButtonHover);
        SetColor(resources, "ColorSwitchKnob", palette.SwitchKnob);
        SetColor(resources, "ColorGlassSurface", palette.GlassSurface);
        SetColor(resources, "ColorGlassBorder", palette.GlassBorder);
        SetColor(resources, "ColorGlassHighlight", palette.GlassHighlight);
        SetColor(resources, "ColorTitleBarBg", palette.TitleBarBg);
        SetColor(resources, "ColorTitleBarBorder", palette.TitleBarBorder);
        SetColor(resources, "ColorIconBlue", palette.IconBlueBg);
        SetColor(resources, "ColorIconCyan", palette.IconCyanBg);
        SetColor(resources, "ColorIconGreen", palette.IconGreenBg);
        SetColor(resources, "ColorIconPurple", palette.IconPurpleBg);
        SetColor(resources, "ColorIconOrange", palette.IconOrangeBg);
        SetColor(resources, "ColorGlassPillBg", palette.GlassPillBg);
        SetColor(resources, "ColorGlassPillBorder", palette.GlassPillBorder);

        SetBrush(resources, "WindowBrush", palette.Window);
        SetBrush(resources, "SurfaceBrush", palette.Surface);
        SetBrush(resources, "SurfaceAltBrush", palette.SurfaceAlt);
        SetBrush(resources, "SurfaceHoverBrush", palette.SurfaceHover);
        SetBrush(resources, "BorderBrushApple", palette.Border);
        SetBrush(resources, "BorderSoftBrush", palette.BorderSoft);
        SetBrush(resources, "PrimaryBrush", palette.Primary);
        SetBrush(resources, "PrimaryHoverBrush", palette.PrimaryHover);
        SetBrush(resources, "CyanBrush", palette.Cyan);
        SetBrush(resources, "SuccessBrush", palette.Success);
        SetBrush(resources, "WarningBrush", palette.Warning);
        SetBrush(resources, "DangerBrush", palette.Danger);
        SetBrush(resources, "TextBrush", palette.Text);
        SetBrush(resources, "TextSecondaryBrush", palette.TextSecondary);
        SetBrush(resources, "TextMutedBrush", palette.TextMuted);
        SetBrush(resources, "StartButtonBrush", palette.StartButton);
        SetBrush(resources, "StartButtonHoverBrush", palette.StartButtonHover);
        SetBrush(resources, "GhostButtonBrush", palette.GhostButton);
        SetBrush(resources, "GhostButtonHoverBrush", palette.GhostButtonHover);
        SetBrush(resources, "SwitchKnobBrush", palette.SwitchKnob);
        SetBrush(resources, "GlassSurfaceBrush", palette.GlassSurface);
        SetBrush(resources, "GlassBorderBrush", palette.GlassBorder);
        SetBrush(resources, "GlassHighlightBrush", palette.GlassHighlight);
        SetBrush(resources, "TitleBarBgBrush", palette.TitleBarBg);
        SetBrush(resources, "TitleBarBorderBrush", palette.TitleBarBorder);
        SetBrush(resources, "IconBlueBrush", palette.IconBlueBg);
        SetBrush(resources, "IconCyanBrush", palette.IconCyanBg);
        SetBrush(resources, "IconGreenBrush", palette.IconGreenBg);
        SetBrush(resources, "IconPurpleBrush", palette.IconPurpleBg);
        SetBrush(resources, "IconOrangeBrush", palette.IconOrangeBg);
        SetBrush(resources, "GlassPillBgBrush", palette.GlassPillBg);
        SetBrush(resources, "GlassPillBorderBrush", palette.GlassPillBorder);
    }

    private static void SetColor(ResourceDictionary resources, string key, MediaColor color)
    {
        ResourceDictionary? dictionary = FindDictionary(resources, key);
        if (dictionary is not null)
        {
            dictionary[key] = color;
        }
    }

    private static void SetBrush(ResourceDictionary resources, string key, MediaColor color)
    {
        ResourceDictionary dictionary = FindDictionary(resources, key) ?? resources;
        dictionary[key] = new SolidColorBrush(color);
    }

    private static ResourceDictionary? FindDictionary(ResourceDictionary resources, string key)
    {
        if (resources.Contains(key))
        {
            return resources;
        }

        foreach (ResourceDictionary merged in resources.MergedDictionaries)
        {
            ResourceDictionary? match = FindDictionary(merged, key);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private sealed record ThemePalette(
        MediaColor Window,
        MediaColor Surface,
        MediaColor SurfaceAlt,
        MediaColor SurfaceHover,
        MediaColor Border,
        MediaColor BorderSoft,
        MediaColor Primary,
        MediaColor PrimaryHover,
        MediaColor Cyan,
        MediaColor Success,
        MediaColor Warning,
        MediaColor Danger,
        MediaColor Text,
        MediaColor TextSecondary,
        MediaColor TextMuted,
        MediaColor StartButton,
        MediaColor StartButtonHover,
        MediaColor GhostButton,
        MediaColor GhostButtonHover,
        MediaColor SwitchKnob,
        MediaColor GlassSurface,
        MediaColor GlassBorder,
        MediaColor GlassHighlight,
        MediaColor TitleBarBg,
        MediaColor TitleBarBorder,
        MediaColor IconBlueBg,
        MediaColor IconCyanBg,
        MediaColor IconGreenBg,
        MediaColor IconPurpleBg,
        MediaColor IconOrangeBg,
        MediaColor GlassPillBg,
        MediaColor GlassPillBorder)
    {
        public static ThemePalette Dark { get; } = new(
            MediaColor.FromRgb(0x0B, 0x0C, 0x0F),
            MediaColor.FromRgb(0x15, 0x17, 0x1C),
            MediaColor.FromRgb(0x1C, 0x1F, 0x26),
            MediaColor.FromRgb(0x24, 0x28, 0x32),
            MediaColor.FromRgb(0x2E, 0x33, 0x3D),
            MediaColor.FromRgb(0x25, 0x2A, 0x33),
            MediaColor.FromRgb(0x4A, 0x8F, 0xE0),
            MediaColor.FromRgb(0x5B, 0x9B, 0xE8),
            MediaColor.FromRgb(0x64, 0xD2, 0xFF),
            MediaColor.FromRgb(0x30, 0xD1, 0x58),
            MediaColor.FromRgb(0xFF, 0xD6, 0x0A),
            MediaColor.FromRgb(0xFF, 0x45, 0x3A),
            MediaColor.FromRgb(0xF5, 0xF5, 0xF7),
            MediaColor.FromRgb(0xA1, 0xA1, 0xAA),
            MediaColor.FromRgb(0x6E, 0x76, 0x81),
            MediaColor.FromRgb(0x4A, 0x8F, 0xE0),
            MediaColor.FromRgb(0x5B, 0x9B, 0xE8),
            MediaColor.FromRgb(0x1C, 0x1F, 0x26),
            MediaColor.FromRgb(0x24, 0x28, 0x32),
            MediaColor.FromRgb(0xFF, 0xFF, 0xFF),
            /* GlassSurface      */ MediaColor.FromRgb(0x1E, 0x1E, 0x24),
            /* GlassBorder       */ MediaColor.FromRgb(0x2C, 0x2C, 0x34),
            /* GlassHighlight    */ MediaColor.FromRgb(0x3A, 0x3A, 0x44),
            /* TitleBarBg        */ MediaColor.FromArgb(0x19, 0xFF, 0xFF, 0xFF),
            /* TitleBarBorder    */ MediaColor.FromArgb(0x10, 0xFF, 0xFF, 0xFF),
            /* IconBlueBg        */ MediaColor.FromRgb(0x1A, 0x3A, 0x5C),
            /* IconCyanBg        */ MediaColor.FromRgb(0x1A, 0x3A, 0x4C),
            /* IconGreenBg       */ MediaColor.FromRgb(0x1A, 0x3A, 0x2C),
            /* IconPurpleBg      */ MediaColor.FromRgb(0x2C, 0x1A, 0x3C),
            /* IconOrangeBg      */ MediaColor.FromRgb(0x3C, 0x2A, 0x1A),
            /* GlassPillBg       */ MediaColor.FromArgb(0x18, 0xFF, 0xFF, 0xFF),
            /* GlassPillBorder   */ MediaColor.FromArgb(0x14, 0xFF, 0xFF, 0xFF));

        public static ThemePalette Light { get; } = new(
            MediaColor.FromRgb(0xF5, 0xF7, 0xFA),
            MediaColor.FromRgb(0xFF, 0xFF, 0xFF),
            MediaColor.FromRgb(0xEE, 0xF1, 0xF6),
            MediaColor.FromRgb(0xE4, 0xE8, 0xF0),
            MediaColor.FromRgb(0xD2, 0xD8, 0xE3),
            MediaColor.FromRgb(0xE3, 0xE7, 0xEF),
            MediaColor.FromRgb(0x4A, 0x8F, 0xE0),
            MediaColor.FromRgb(0x5B, 0x9B, 0xE8),
            MediaColor.FromRgb(0x00, 0x71, 0xE3),
            MediaColor.FromRgb(0x24, 0x8A, 0x3D),
            MediaColor.FromRgb(0xB8, 0x86, 0x0B),
            MediaColor.FromRgb(0xD7, 0x00, 0x15),
            MediaColor.FromRgb(0x10, 0x13, 0x18),
            MediaColor.FromRgb(0x4E, 0x59, 0x69),
            MediaColor.FromRgb(0x7B, 0x84, 0x94),
            MediaColor.FromRgb(0x4A, 0x8F, 0xE0),
            MediaColor.FromRgb(0x5B, 0x9B, 0xE8),
            MediaColor.FromRgb(0xF1, 0xF4, 0xF8),
            MediaColor.FromRgb(0xE7, 0xEC, 0xF3),
            MediaColor.FromRgb(0xFF, 0xFF, 0xFF),
            /* GlassSurface      */ MediaColor.FromRgb(0xFA, 0xFB, 0xFD),
            /* GlassBorder       */ MediaColor.FromRgb(0xE0, 0xE4, 0xEB),
            /* GlassHighlight    */ MediaColor.FromRgb(0xD5, 0xDA, 0xE3),
            /* TitleBarBg        */ MediaColor.FromArgb(0x30, 0xF5, 0xF7, 0xFA),
            /* TitleBarBorder    */ MediaColor.FromArgb(0x12, 0x00, 0x00, 0x00),
            /* IconBlueBg        */ MediaColor.FromRgb(0xE8, 0xF0, 0xFE),
            /* IconCyanBg        */ MediaColor.FromRgb(0xE5, 0xF5, 0xFC),
            /* IconGreenBg       */ MediaColor.FromRgb(0xE5, 0xF5, 0xE8),
            /* IconPurpleBg      */ MediaColor.FromRgb(0xF0, 0xE5, 0xF8),
            /* IconOrangeBg      */ MediaColor.FromRgb(0xF8, 0xED, 0xE5),
            /* GlassPillBg       */ MediaColor.FromArgb(0x0E, 0x10, 0x13, 0x18),
            /* GlassPillBorder   */ MediaColor.FromArgb(0x0C, 0x00, 0x00, 0x00));
    }
}
