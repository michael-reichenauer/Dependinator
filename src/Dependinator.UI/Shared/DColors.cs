namespace Dependinator.UI.Shared;

class DColors
{
    static readonly string CanvasBackgroundDark = "#0D0F11";
    static readonly string CanvasBackgroundLight = "#FAFAFA";

    // Item (node and line)
    static readonly string ItemColorDarkSelected = "#4DA6FF";
    static readonly string ItemColorLightSelected = "#1A73E8";
    static readonly string ItemLineSelected = "#a7cff7";

    // Tests
    static readonly string TextDark = "#FFFFFF";
    static readonly string TextLight = "#1A1A1A";

    // Line Dark
    static readonly string LineDark = "#999999";
    static readonly string LineHiddenDark = "#2A2D30";

    // Line Light
    static readonly string LineLight = "#555555";
    static readonly string LineHiddenLight = "#DDDDDD";

    // Direct line
    static readonly string DirectLineDark = "#7C4DFF";
    static readonly string DirectLineLight = "#512DA8";

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

    static readonly IReadOnlyList<DColor> nodeColorsDark =
    [
        new("Gray", "#CCCCCC", "#1A1A1A"),
        new("Blue", "#4DA6FF", "#0D1826"),
        new("Cyan", "#33CCCC", "#0D1F1F"),
        new("Teal", "#2ECC71", "#0D1A12"),
        new("Green", "#A3E635", "#1A220D"),
        new("Yellow", "#FFD633", "#1F1A0D"),
        new("Orange", "#FF944D", "#1F120D"),
        new("Red", "#FF4D4D", "#1A0D0D"),
        new("Pink", "#FF66B2", "#1A0D12"),
        new("Purple", "#B366FF", "#120D1A"),
    ];

    static readonly IReadOnlyList<DColor> nodeColorsLight =
    [
        new("Gray", "#4D4D4D", "#FFFFFF"),
        new("Blue", "#004C99", "#F9FCFF"),
        new("Cyan", "#008080", "#F9FFFF"),
        new("Teal", "#0D6640", "#F9FFFD"),
        new("Green", "#5C8001", "#FDFFFA"),
        new("Yellow", "#996600", "#FFFEFA"),
        new("Orange", "#994C00", "#FFFBF9"),
        new("Red", "#990000", "#FFFAFA"),
        new("Pink", "#99334D", "#FFFAFD"),
        new("Purple", "#4C0080", "#FCF9FF"),
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
    static readonly string CustomBackgroundDark = "#12243F";
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
    public static string ManualMarker => IsDark ? ManualMarkerDark : ManualMarkerLight;
    public static string NoteFill => IsDark ? NoteFillDark : NoteFillLight;
    public static string NoteBorder => IsDark ? NoteBorderDark : NoteBorderLight;
    public static string NoteText => "#FFFFFF";
    public static string EditNodeBorder => IsDark ? NodeBorderDarkEdit : NodeBorderLightEdit;
    public static string EditNodeBackground => IsDark ? NodeBackgroundDarkEdit : NodeBackgroundLightEdit;
    public static string Text => IsDark ? TextDark : TextLight;

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
