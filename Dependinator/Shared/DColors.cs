namespace Dependinator.Shared;

class DColors
{
    static readonly string CanvasBackgroundDark = "#0D0F11";
    static readonly string CanvasBackgroundLight = "#FAFAFA";

    // Item (node and line)
    static readonly string ItemColorDarkSelected = "#4DA6FF";
    static readonly string ItemColorLightSelected = "#1A73E8";

    // Tests
    static readonly string TextDark = "#FFFFFF";
    static readonly string TextLight = "#1A1A1A";

    // Line Dark
    static readonly string LineDark = "#999999";
    static readonly string LineHiddenDark = "#2A2D30";

    // Line Light
    static readonly string LineLight = "#555555";
    static readonly string LineHiddenLight = "#DDDDDD";

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

    public static bool IsDark { get; set; } = false;
    static readonly Random random = new();

    public static readonly string CanvasBackground = IsDark ? CanvasBackgroundDark : CanvasBackgroundLight;
    public static readonly string Line = IsDark ? LineDark : LineLight;
    public static readonly string LineHidden = IsDark ? LineHiddenDark : LineHiddenLight;
    public static readonly string EditNodeBorder = IsDark ? NodeBorderDarkEdit : NodeBorderLightEdit;
    public static readonly string EditNodeBackground = IsDark ? NodeBackgroundDarkEdit : NodeBackgroundLightEdit;
    public static readonly string Text = IsDark ? TextDark : TextLight;

    public static readonly string Selected = IsDark ? ItemColorDarkSelected : ItemColorLightSelected;
    public static readonly string ToolBarIcon = MudBlazor.Colors.DeepPurple.Lighten5;

    public static string RandomNodeColorName() => nodeColorsDark[random.Next(nodeColorsDark.Count)].Name;

    public static (string color, string background) NodeColorByName(string name)
    {
        var colors = IsDark ? nodeColorsDark : nodeColorsLight;
        var color = colors.FirstOrDefault(c => c.Name == name) ?? colors[0];
        return (color.Border, color.Background);
    }
}

record DColor(string Name, string Border, string Background);
