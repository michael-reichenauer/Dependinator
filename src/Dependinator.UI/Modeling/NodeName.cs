using System.Text;
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
        var parts = Parsing.Utils.NodeName.SplitParts(nodeName);

        // A member's short name leaves out its parameters. A type has none, except a C# extension
        // block, which is named after its receiver: "extension(NodeType)".
        string namePart = nodeType.IsType ? parts[^1] : parts[^1].Split('(')[0];
        string shortName = ToNiceParameters(ToNiceText(namePart));

        var subParts = nodeName.StartsWith('$') ? parts : parts.Skip(1);
        string longName = string.Join(".", subParts.Where(part => !part.StartsWith('?')));

        if (string.IsNullOrEmpty(longName))
        {
            longName = ToNiceText(nodeName);
        }
        else
        {
            longName = ToNiceParameters(ToNiceText(longName));
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

    // The type names in parameter lists, indexer parameters and generic arguments without their
    // namespaces: "Do(System.Int32,Ns.Item)" reads "Do(Int32,Item)". The dots outside brackets
    // separate the name's parts and stay.
    static string ToNiceParameters(string name)
    {
        var text = new StringBuilder(name.Length);
        var depth = 0;
        var bracketsStart = 0;
        for (var i = 0; i < name.Length; i++)
        {
            switch (name[i])
            {
                case '<' or '(' or '[':
                    if (depth == 0)
                    {
                        text.Append(name, bracketsStart, i + 1 - bracketsStart);
                        bracketsStart = i + 1;
                    }
                    depth++;
                    break;
                case '>' or ')' or ']' when depth > 0:
                    depth--;
                    if (depth == 0)
                    {
                        text.Append(Qualifiers().Replace(name[bracketsStart..i], ""));
                        bracketsStart = i;
                    }
                    break;
            }
        }

        var rest = name[bracketsStart..];
        return text.Append(depth > 0 ? Qualifiers().Replace(rest, "") : rest).ToString();
    }

    // "global::Ns.Sub." in "global::Ns.Sub.Item"
    [GeneratedRegex(@"(global::)?(\w+\.)+")]
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
