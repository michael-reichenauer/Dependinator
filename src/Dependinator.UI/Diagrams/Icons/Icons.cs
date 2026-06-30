namespace Dependinator.UI.Diagrams.Icons;

// Facade over the icon library. The full node icons live as embedded .svg files under
// Diagrams/Icons/Library/ and are resolved (and cached) via IconLibrary. The small inline glyphs
// below are MudBlazor toolbar/connection control icons, not part of the swappable node library.
static class Icon
{
    public const string DependenciesIcon = MudBlazor.Icons.Material.Outlined.Polyline;
    public const string ReferencesIcon =
        "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g><g transform=\"rotate(180,12,12)\"><path d=\"M15,16v1.26l-6-3v-3.17L11.7,8H16V2h-6v4.9L7.3,10H3v6h5l7,3.5V22h6v-6H15z M12,4h2v2h-2V4z M7,14H5v-2h2V14z M19,20h-2v-2 h2V20z\"/></g>";
    public const string DirectConnection =
        "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g><g><rect x=\"3.5\" y=\"9.5\" width=\"5\" height=\"5\" rx=\"1\" fill=\"currentColor\"/><rect x=\"15.5\" y=\"9.5\" width=\"5\" height=\"5\" rx=\"1\" fill=\"currentColor\"/><rect x=\"8\" y=\"11.25\" width=\"8\" height=\"1.5\" rx=\"0.75\" fill=\"currentColor\"/></g>";
    public const string LineSourceIcon =
        "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g><g><circle cx=\"6\" cy=\"12\" r=\"2.4\" fill=\"currentColor\"/><rect x=\"8\" y=\"11\" width=\"12\" height=\"2\" rx=\"1\" fill=\"currentColor\"/></g>";
    public const string LineTargetIcon =
        "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g><g><rect x=\"4\" y=\"11\" width=\"12\" height=\"2\" rx=\"1\" fill=\"currentColor\"/><polygon points=\"16,8.5 22,12 16,15.5\" fill=\"currentColor\"/></g>";

    internal static string ModuleIcon => IconLibrary.Get("ModuleIcon");

    static readonly Dictionary<Parsing.NodeType, string> IconMap = new()
    {
        { Parsing.NodeType.Solution, "SolutionIcon" },
        { Parsing.NodeType.Externals, "ExternalsIcon" },
        { Parsing.NodeType.Type, "TypeIcon" },
        { Parsing.NodeType.ClassType, "TypeIcon" },
        { Parsing.NodeType.InterfaceType, "InterfaceIcon" },
        { Parsing.NodeType.EnumType, "EnumIcon" },
        { Parsing.NodeType.StructType, "StructIcon" },
        { Parsing.NodeType.RecordType, "RecordIcon" },
        { Parsing.NodeType.MethodMember, "MemberIcon" },
        { Parsing.NodeType.FieldMember, "MemberIcon" },
        { Parsing.NodeType.ConstructorMember, "MemberIcon" },
        { Parsing.NodeType.EventMember, "MemberIcon" },
        { Parsing.NodeType.PropertyMember, "MemberIcon" },
    };

    public static string GetIcon(Parsing.NodeType iconName)
    {
        if (!IconMap.TryGetValue(iconName, out string? name))
            return ModuleIcon;

        return IconLibrary.Get(name);
    }

    // All node icon definitions, rendered once into the diagram's <defs> so nodes can reference
    // them by id via <use href="#name">.
    public static string IconDefs => IconLibrary.Defs;
}
