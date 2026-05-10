// Emulator-side theme. Mirrors DmnwareTheme.ApplyDefaults() in /AppGen/DmnwareTheme.cs
// without dragging the ScriptableObject + AssetDatabase machinery.

using Avalonia.Media;
using DMNware.AppGen;

namespace YtsEmulator.Layout;

public static class Theme
{
    public static readonly Color Background = Rgb(0.07f, 0.08f, 0.10f);
    public static readonly Color Surface = Rgb(0.12f, 0.13f, 0.16f);
    public static readonly Color SurfaceElevated = Rgb(0.16f, 0.18f, 0.22f);
    public static readonly Color Primary = Rgb(0.25f, 0.55f, 1.00f);
    public static readonly Color OnPrimary = Color.FromRgb(255, 255, 255);
    public static readonly Color Text = Rgb(0.95f, 0.96f, 0.98f);
    public static readonly Color TextMuted = Rgb(0.65f, 0.66f, 0.70f);
    public static readonly Color Accent = Rgb(0.45f, 0.85f, 1.00f);
    public static readonly Color Danger = Rgb(0.90f, 0.30f, 0.30f);
    public static readonly Color OnDanger = Color.FromRgb(255, 255, 255);

    public const double BodyFontSize = 32;     // scaled-up for the 1080-wide native canvas
    public const double HeaderFontSize = 56;
    public const double CaptionFontSize = 24;

    public const double ButtonHeight = 96;
    public const double SliderHeight = 64;
    public const double ToggleWidth = 110;
    public const double ToggleHeight = 64;
    public const double DividerThickness = 2;
    public const double CornerRadius = 16;

    public static (Color bg, Color fg) ButtonColors(Variant v) => v switch
    {
        Variant.Primary => (Primary, OnPrimary),
        Variant.Secondary => (SurfaceElevated, Accent),
        Variant.Ghost => (Color.FromArgb(0, 0, 0, 0), Text),
        Variant.Danger => (Danger, OnDanger),
        _ => (Surface, Text),
    };

    public static double TypographySize(Typography t) => t switch
    {
        Typography.Header => HeaderFontSize,
        Typography.Caption => CaptionFontSize,
        _ => BodyFontSize,
    };

    public static FontWeight TypographyWeight(Typography t) => t switch
    {
        Typography.Header => FontWeight.Bold,
        _ => FontWeight.Normal,
    };

    public static Color TypographyColor(Typography t) => t switch
    {
        Typography.Caption => TextMuted,
        _ => Text,
    };

    private static Color Rgb(float r, float g, float b)
        => Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));

    public static Color FromUnity(UnityEngine.Color c)
        => Color.FromArgb((byte)(c.a * 255), (byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255));

    public static SolidColorBrush BrushFromUnity(UnityEngine.Color c) => new(FromUnity(c));
}
