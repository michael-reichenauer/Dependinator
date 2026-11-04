namespace Dependinator.UI.Shared;

class DColors
{
    // A very dark grey rather than black: lighter greys (#1E1F22 and up) were tried and looked
    // misty, and pure black makes the colored frames and white text glare.
    static readonly string CanvasBackgroundDark = "#101113";
    static readonly string CanvasBackgroundLight = "#FAFAFA";

    // Item (node and line)
    static readonly string ItemColorDarkSelected = "#4DA6FF";
    static readonly string ItemColorLightSelected = "#1A73E8";
    static readonly string ItemLineSelected = "#a7cff7";

    // The member card behind a container's member rows, and the summary card of a type: a
    // paper-like panel on the container's tint, with quiet headings and dividers.
    static readonly string CardBackgroundDark = "#1A1B1F";
    static readonly string CardBackgroundLight = "#FFFFFF";
    static readonly string CardBorderDark = "#34363D";
    static readonly string CardBorderLight = "#D9D7E0";
    static readonly string CardDividerDark = "#2B2D33";
    static readonly string CardDividerLight = "#E8E6EE";
    static readonly string CardMutedTextDark = "#9C99AA";
    static readonly string CardMutedTextLight = "#6E6A7C";

    // Tests
    static readonly string TextDark = "#ECECEC";
    static readonly string TextLight = "#1A1A1A";

    // Line Dark
    static readonly string LineDark = "#969AA1";
    static readonly string LineHiddenDark = "#282A2E";

    // Line Light
    static readonly string LineLight = "#555555";
    static readonly string LineHiddenLight = "#DDDDDD";

    // Direct line
    static readonly string DirectLineDark = "#7C4DFF";
    static readonly string DirectLineLight = "#512DA8";

    // Lines that are part of a circular dependency (View › Show Cycles): a warning red that
    // reads on both canvases.
    static readonly string CycleLineDark = "#FF6B6B";
    static readonly string CycleLineLight = "#C62828";

    // Lines on the dependency path the user asked for (View › Find Path): a cyan that is unlike
    // the purple direct lines and the red cycle lines on both canvases.
    static readonly string PathLineDark = "#4DD0E1";
    static readonly string PathLineLight = "#00838F";

    // Lines that break an architecture rule (View › Show Architecture Rules): an orange, warm like
    // the cycle red but told apart from it.
    static readonly string RuleLineDark = "#FFB74D";
    static readonly string RuleLineLight = "#E65100";

    // Cousin (container-crossing) lines: slightly toned down from the normal line color so they
    // read as "crossing" without cluttering dense views. Move these toward LineDark/LineLight
    // (#969AA1 / #555555) to make them more visible, or set them equal for no distinction.
    static readonly string CousinLineDark = "#82868D";
    static readonly string CousinLineLight = "#6E727A";

    // Marker glyph on manually added (user-drawn) nodes — neutral grey so it reads as
    // "hand-drawn/editable" rather than a status indicator.
    static readonly string ManualMarkerDark = "#8A8F98";
    static readonly string ManualMarkerLight = "#5F6368";

    // Note annotation circles — a distinct accent (blue) so notes read as a separate guidance
    // layer over the diagram. Text is white for contrast on the filled circle.
    static readonly string NoteFillDark = "#4DA6FF";
    static readonly string NoteFillLight = "#1A73E8";
    static readonly string NoteBorderDark = "#1A2A40";
    static readonly string NoteBorderLight = "#0D3B66";

    // Node Dark
    // static readonly string NodeBackgroundDarkSelected = "#1A2A40";
    static readonly string NodeBorderDarkEdit = "#FFA64D";
    static readonly string NodeBackgroundDarkEdit = "#332312";

    // The dark counterpart of the light palette below: soft pastel borders (HSL saturation 62%,
    // lightness 66%) rather than full-strength colors, which glare on a dark canvas (a bright red
    // frame reads as an error), and backgrounds a small step lighter than the canvas with a
    // hue (saturation 25%, lightness 11%), so containers read as tinted panels, not dark stains.
    static readonly IReadOnlyList<DColor> nodeColorsDark =
    [
        new("Gray", "#BDBDBD", "#1A1B1E"),
        new("Blue", "#73A8DE", "#151C23"),
        new("Cyan", "#73DEDE", "#152323"),
        new("Teal", "#73DE9F", "#15231B"),
        new("Green", "#B1DE73", "#1D2315"),
        new("Yellow", "#DEC973", "#232015"),
        new("Orange", "#DE9E73", "#231B15"),
        new("Red", "#DE7373", "#231515"),
        new("Pink", "#DE73A8", "#23151C"),
        new("Purple", "#A873DE", "#1C1523"),
    ];

    // Backgrounds are pastel tints a small step below the near-white canvas (~1.05:1 contrast
    // against it, all hues alike); the high-luminance hues (cyan..yellow) are less saturated so
    // they read about as strong as the others rather than louder.
    static readonly IReadOnlyList<DColor> nodeColorsLight =
    [
        new("Gray", "#4D4D4D", "#F5F5F5"),
        new("Blue", "#004C99", "#EDF6FF"),
        new("Cyan", "#008080", "#E0FAFA"),
        new("Teal", "#0D6640", "#E2FAF2"),
        new("Green", "#5C8001", "#EDF9DC"),
        new("Yellow", "#996600", "#FBF5DC"),
        new("Orange", "#994C00", "#FFF2ED"),
        new("Red", "#990000", "#FFF2F2"),
        new("Pink", "#99334D", "#FFF1F8"),
        new("Purple", "#4C0080", "#F9F2FF"),
    ];

    // Node Light
    // static readonly string NodeBackgroundLightSelected = "#E6F0FF";
    static readonly string NodeBorderLightEdit = "#CC7000";
    static readonly string NodeBackgroundLightEdit = "#FFF2E0";

    // User-selected container colors: the shared accent colors (see ColorUtil, same six as the
    // icon tints) rendered in the same style as the auto palette above, derived per theme by
    // re-hueing the canonical "Blue" entry (hue ~210), but with a slightly stronger background
    // than the auto palette so an explicit color choice stands out against the canvas. Names
    // deliberately overlap the auto palette's — resolution is separate (CustomNodeColorByName
    // vs NodeColorByName).
    const double CustomBaseHue = 210;
    static readonly string CustomBackgroundDark = "#172235";
    static readonly string CustomBackgroundLight = "#EAF3FF";

    static readonly IReadOnlyList<DColor> customNodeColorsDark = DeriveCustomColors(
        nodeColorsDark[1] with
        {
            Background = CustomBackgroundDark,
        }
    );
    static readonly IReadOnlyList<DColor> customNodeColorsLight = DeriveCustomColors(
        nodeColorsLight[1] with
        {
            Background = CustomBackgroundLight,
        }
    );

    static IReadOnlyList<DColor> DeriveCustomColors(DColor blue) =>
        [
            .. ColorUtil.AccentColors.Select(accent => new DColor(
                accent.Name,
                ColorUtil.ShiftHue(blue.Border, CustomBaseHue, accent.Hue),
                ColorUtil.ShiftHue(blue.Background, CustomBaseHue, accent.Hue)
            )),
        ];

    // Computed properties (not readonly fields) so a future IsDark switch takes effect at
    // runtime; readonly fields would be frozen to the light palette at type initialization.
    public static bool IsDark { get; set; } = false;
    public static string CanvasBackground => IsDark ? CanvasBackgroundDark : CanvasBackgroundLight;
    public static string Line => IsDark ? LineDark : LineLight;
    public static string LineHidden => IsDark ? LineHiddenDark : LineHiddenLight;
    public static string DirectLine => IsDark ? DirectLineDark : DirectLineLight;
    public static string CycleLine => IsDark ? CycleLineDark : CycleLineLight;
    public static string PathLine => IsDark ? PathLineDark : PathLineLight;
    public static string RuleLine => IsDark ? RuleLineDark : RuleLineLight;
    public static string CousinLine => IsDark ? CousinLineDark : CousinLineLight;
    public static string ManualMarker => IsDark ? ManualMarkerDark : ManualMarkerLight;

    // Problem badges on nodes (white count on top), the problem indicator's error and warning.
    public static string ProblemError => "#D32F2F";
    public static string ProblemWarning => "#E65100";
    public static string HiddenBadge => "#757575"; // Neutral: hidden nodes are not a problem
    public static string NoteFill => IsDark ? NoteFillDark : NoteFillLight;
    public static string NoteBorder => IsDark ? NoteBorderDark : NoteBorderLight;
    public static string NoteText => "#FFFFFF";
    public static string EditNodeBorder => IsDark ? NodeBorderDarkEdit : NodeBorderLightEdit;
    public static string EditNodeBackground => IsDark ? NodeBackgroundDarkEdit : NodeBackgroundLightEdit;
    public static string Text => IsDark ? TextDark : TextLight;
    public static string CardBackground => IsDark ? CardBackgroundDark : CardBackgroundLight;
    public static string CardBorder => IsDark ? CardBorderDark : CardBorderLight;
    public static string CardDivider => IsDark ? CardDividerDark : CardDividerLight;
    public static string CardMutedText => IsDark ? CardMutedTextDark : CardMutedTextLight;

    public static string Selected => IsDark ? ItemColorDarkSelected : ItemColorLightSelected;
    public static string LineSelected => ItemLineSelected;
    public static string ToolBarIcon => MudBlazor.Colors.DeepPurple.Lighten5;

    public static string ColorBasedOnName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return nodeColorsDark[0].Name;

        uint hash = GetHash(name);
        var index = (int)(hash % (uint)nodeColorsDark.Count);
        return nodeColorsDark[index].Name;
    }

    public static (string color, string background) NodeColorByName(string name)
    {
        var colors = IsDark ? nodeColorsDark : nodeColorsLight;
        var color = colors.FirstOrDefault(c => c.Name == name) ?? colors[0];
        return (color.Border, color.Background);
    }

    // The user-selectable container colors for the current theme, for rendering color pickers.
    public static IReadOnlyList<DColor> CustomNodeColors => IsDark ? customNodeColorsDark : customNodeColorsLight;

    // Whether name is one of the user-selectable container colors, so consumers can validate
    // persisted names and fall back to the node's auto color for stale/unknown ones.
    public static bool IsCustomNodeColor(string name) => customNodeColorsDark.Any(c => c.Name == name);

    public static (string color, string background) CustomNodeColorByName(string name)
    {
        var colors = IsDark ? customNodeColorsDark : customNodeColorsLight;
        var color = colors.FirstOrDefault(c => c.Name == name) ?? colors[0];
        return (color.Border, color.Background);
    }

    static uint GetHash(string name)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        uint hash = offsetBasis;
        foreach (var character in name)
        {
            hash ^= char.ToUpperInvariant(character);
            hash *= prime;
        }

        return hash;
    }
}

record DColor(string Name, string Border, string Background);
