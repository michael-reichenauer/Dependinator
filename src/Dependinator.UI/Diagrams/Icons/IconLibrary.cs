using System.Reflection;

// The built-in icon library: loads and caches node icons from embedded .svg resources for use
// in the diagram.
namespace Dependinator.UI.Diagrams.Icons;

// One icon read from an embedded .svg resource. Name equals the svg's `id` attribute and is the
// key used by `<use href="#name">` references; Group is the containing library folder (e.g. "Default");
// DisplayName is the user-facing name shown in icon lists (separators converted to spaces).
readonly record struct IconInfo(string Group, string Name, string DisplayName, string Svg);

// Loads and caches the built-in icon library from embedded `.svg` resources
// (src/Dependinator.UI/Diagrams/Icons/Library/<Group>/<Name>.svg).
//
// This is the single lookup seam for icons: every consumer goes through Get/Defs/All. A future
// user-provided icon source can be layered in here without touching any consumer.
static class IconLibrary
{
    const string Fallback = "Module";

    // Marker identifying the embedded icon resources, e.g.
    // "Dependinator.UI.Diagrams.Icons.Library.Default.Solution.svg".
    const string LibraryMarker = ".Library.";

    static readonly Lazy<Cache> cache = new(Load);

    public static string Get(string name)
    {
        var byName = cache.Value.ByName;
        if (byName.TryGetValue(name, out var svg))
            return svg;
        return byName.TryGetValue(Fallback, out var fallback) ? fallback : "";
    }

    // Whether an icon with the given name exists, so consumers can validate persisted icon
    // names and fall back to the node-type default for stale/unknown ones.
    public static bool Contains(string name) => cache.Value.ByName.ContainsKey(name);

    // All icon svgs concatenated, for rendering once into the diagram's <defs> so nodes can
    // reference them by id via <use href="#name">.
    public static string Defs => cache.Value.Defs;

    // The full library, for building grouped icon lists (e.g. a future icon picker).
    public static IReadOnlyList<IconInfo> All => cache.Value.All;

    static Cache Load()
    {
        var assembly = typeof(IconLibrary).Assembly;
        var icons = new List<IconInfo>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.Contains(LibraryMarker) || !resourceName.EndsWith(".svg"))
                continue;

            if (TryParse(resourceName, ReadResource(assembly, resourceName), out var icon))
                icons.Add(icon);
        }

        var byName = icons.ToDictionary(i => i.Name, i => i.Svg);
        var defs = string.Join("\n", icons.Select(i => i.Svg));
        return new Cache(byName, defs, icons);
    }

    static bool TryParse(string resourceName, string svg, out IconInfo icon)
    {
        // "...Library.Default.Solution.svg" -> group "Default", name "Solution".
        var relative = resourceName[(resourceName.IndexOf(LibraryMarker) + LibraryMarker.Length)..];
        relative = relative[..^".svg".Length];

        var split = relative.LastIndexOf('.');
        if (split <= 0)
        {
            icon = default;
            return false;
        }

        var group = relative[..split];
        var name = relative[(split + 1)..];
        icon = new IconInfo(group, name, ToDisplayName(name), svg);
        return true;
    }

    // Icon library file names can contain '-'/'_' separators (common in e.g. Azure/AWS icon
    // sets); the display name shown in icon lists uses spaces instead.
    internal static string ToDisplayName(string name) => name.Replace('-', ' ').Replace('_', ' ').Trim();

    static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    sealed record Cache(IReadOnlyDictionary<string, string> ByName, string Defs, IReadOnlyList<IconInfo> All);
}
