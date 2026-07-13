namespace Dependinator.UI.Shared;

// The user-selectable accent colors and the hue-shift math behind them. Both icon tints
// (IconLibrary) and container colors (DColors) offer the same six accents, each derived by
// re-hueing a canonical color of its own base palette, so the two pickers stay one mental model
// while every derived color keeps its palette's saturation/lightness style.
static class ColorUtil
{
    // Colors below this saturation are neutral detail marks (white, gray) and are kept as-is
    // when re-hueing.
    const double NeutralSaturation = 0.05;

    // The Gray accent desaturates instead of re-hueing (a null hue below).
    const double GraySaturationScale = 0.15;

    // The selectable accents besides each palette's own default; a null hue means desaturate.
    public static readonly IReadOnlyList<(string Name, double? Hue)> AccentColors =
    [
        ("Blue", 215),
        ("Teal", 180),
        ("Green", 145),
        ("Amber", 40),
        ("Rose", 340),
        ("Gray", null),
    ];

    // Re-hues one #RGB/#RRGGBB color from its palette's base hue to the target (or desaturates
    // it when the target is null), keeping saturation and lightness; near-neutral colors
    // (white/gray details) are returned unchanged.
    public static string ShiftHue(string hex, double baseHue, double? targetHue)
    {
        var (r, g, b) = ParseHex(hex);
        var (h, s, l) = RgbToHsl(r, g, b);
        if (s < NeutralSaturation)
            return hex;

        (h, s) = targetHue is { } hue ? (((h + hue - baseHue) % 360 + 360) % 360, s) : (h, s * GraySaturationScale);
        var (nr, ng, nb) = HslToRgb(h, s, l);
        return $"#{nr:X2}{ng:X2}{nb:X2}";
    }

    static (int R, int G, int B) ParseHex(string hex)
    {
        var value = hex[1..];
        if (value.Length == 3)
            value = $"{value[0]}{value[0]}{value[1]}{value[1]}{value[2]}{value[2]}";
        return (Convert.ToInt32(value[..2], 16), Convert.ToInt32(value[2..4], 16), Convert.ToInt32(value[4..6], 16));
    }

    static (double H, double S, double L) RgbToHsl(int red, int green, int blue)
    {
        var r = red / 255.0;
        var g = green / 255.0;
        var b = blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;

        if (max == min)
            return (0, 0, l);

        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        var h =
            max == r ? ((g - b) / d + (g < b ? 6 : 0)) * 60
            : max == g ? ((b - r) / d + 2) * 60
            : ((r - g) / d + 4) * 60;
        return (h, s, l);
    }

    static (int R, int G, int B) HslToRgb(double h, double s, double l)
    {
        if (s == 0)
        {
            var value = (int)Math.Round(l * 255);
            return (value, value, value);
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        return (Channel(p, q, h + 120), Channel(p, q, h), Channel(p, q, h - 120));
    }

    static int Channel(double p, double q, double hue)
    {
        hue = (hue % 360 + 360) % 360;
        var value =
            hue < 60 ? p + (q - p) * hue / 60
            : hue < 180 ? q
            : hue < 240 ? p + (q - p) * (240 - hue) / 60
            : p;
        return (int)Math.Round(value * 255);
    }
}
