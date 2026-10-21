using System.Text.RegularExpressions;
using Dependinator.Core.Parsing;

namespace Dependinator.UI.Modeling;

static partial class NodeName
{
    public static (string longName, string shortName) GetDisplayNames(
        string nodeName,
        NodeType nodeType,
        bool isExecutable
    )
    {
        IReadOnlyList<string> parts = Parsing.Utils.NodeName.SplitParts(nodeName);

        // A member's short name leaves out its parameters. Other nodes have none, but their names
        // can have brackets too: a C# extension block is named after its receiver,
        // "extension(NodeType)", and a manual node can be named "Cache (v2)".
        string namePart = nodeType.IsMember ? parts[^1].Split('(')[0] : parts[^1];
        string shortName = ToNiceText(WithoutQualifiers(namePart));

        var subParts = nodeName.StartsWith('$') ? parts : parts.Skip(1);
        string longName = string.Join(".", subParts.Where(part => !part.StartsWith('?')).Select(WithoutQualifiers));

        if (string.IsNullOrEmpty(longName))
        {
            longName = ToNiceText(nodeName);
        }
        else
        {
            longName = ToNiceText(longName);
        }

        if (nodeType == NodeType.Assembly && !shortName.EndsWith(".dll") && !shortName.EndsWith(".exe"))
        {
            shortName += ".dll";
            longName += ".dll";
        }

        shortName = FormatModuleSuffix(shortName, isExecutable);
        longName = FormatModuleSuffix(longName, isExecutable);

        return (longName, shortName);
    }

    // Module names are shown as "Name (dll)" or "Name (exe)" instead of "Name.dll". SDK-built
    // executables compile to a ".dll" module (the ".exe" is just the apphost), so IsExecutable
    // decides the suffix rather than the module name's extension.
    static string FormatModuleSuffix(string name, bool isExecutable)
    {
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return $"{name[..^4]} (exe)";
        if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return $"{name[..^4]} ({(isExecutable ? "exe" : "dll")})";

        return name;
    }

    // The type names in a part's brackets (parameter lists, indexer parameters, generic arguments,
    // an extension block's receiver) without their namespaces: "Do(System.Int32,global::Ns.Item)"
    // reads "Do(Int32,Item)". A part has dots only inside its brackets: the other dots separate
    // the parts. Before ToNiceText, which turns the "*" of encoded names into dots.
    static string WithoutQualifiers(string part) => Qualifiers().Replace(part, "");

    // "global::Ns.Sub." in "global::Ns.Sub.Item" and "global::" in "global::Item" (a type in the
    // global namespace), but not a module named "global::"
    [GeneratedRegex(@"global::(\w+\.)*(?=\w)|(\w+\.)+")]
    private static partial Regex Qualifiers();

    static string ToNiceText(string name) =>
        name.Replace("*", ".")
            .Replace("#", ".")
            .Replace("?", "")
            .Replace("$", "")
            .Replace("%", "")
            .Replace("/", ".")
            .Replace("op_Equality", "==")
            .Replace("op_Inequality", "!=");
}
