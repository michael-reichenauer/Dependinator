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

    internal static string ModuleIcon => IconLibrary.Get("Module");

    static readonly Dictionary<Parsing.NodeType, string> IconMap = new()
    {
        { Parsing.NodeType.Solution, "Solution" },
        { Parsing.NodeType.Externals, "Externals" },
        { Parsing.NodeType.Assembly, "Assembly" },
        { Parsing.NodeType.Namespace, "Namespace" },
        // The Roslyn source parser doesn't emit Namespace nodes; namespace containers are
        // rebuilt as implicit Parent nodes (see StructureService.GetOrCreateParent), so Parent
        // also renders as a namespace. (The Files icon is kept in the library for future use.)
        { Parsing.NodeType.Parent, "Namespace" },
        { Parsing.NodeType.Type, "Type" },
        { Parsing.NodeType.ClassType, "Type" },
        { Parsing.NodeType.InterfaceType, "Interface" },
        { Parsing.NodeType.EnumType, "Enum" },
        { Parsing.NodeType.StructType, "Struct" },
        { Parsing.NodeType.RecordType, "Record" },
        { Parsing.NodeType.MethodMember, "Method" },
        { Parsing.NodeType.FieldMember, "Field" },
        { Parsing.NodeType.ConstructorMember, "Constructor" },
        { Parsing.NodeType.EventMember, "Event" },
        { Parsing.NodeType.PropertyMember, "Property" },
    };

    // The name of the default icon for a node type (an IconLibrary id), falling back to "Module".
    public static string GetIconName(Parsing.NodeType nodeType) =>
        IconMap.TryGetValue(nodeType, out string? name) ? name : "Module";

    public static string GetIcon(Parsing.NodeType nodeType) => IconLibrary.Get(GetIconName(nodeType));

    // The icon name for a node, honoring a user-selected custom icon and icon color; unknown
    // (e.g. stale persisted) names or colors fall back to the node-type default/base icon.
    public static string GetIconName(Modeling.Models.Node node)
    {
        var name =
            node.CustomIconName is { } customIconName && IconLibrary.Contains(customIconName)
                ? customIconName
                : GetIconName(node.Type);

        if (node.CustomIconColor is { } color && IconLibrary.Contains(IconLibrary.VariantName(name, color)))
            return IconLibrary.VariantName(name, color);

        return name;
    }

    // Icon for a node, honoring a user-selected custom icon.
    public static string GetIcon(Modeling.Models.Node node) => IconLibrary.Get(GetIconName(node));

    // All node icon definitions, rendered once into the diagram's <defs> so nodes can reference
    // them by id via <use href="#name">.
    public static string IconDefs => IconLibrary.Defs;
}
