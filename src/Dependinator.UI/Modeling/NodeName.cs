using Dependinator.Core.Parsing;

namespace Dependinator.UI.Modeling;

static class NodeName
{
    static readonly char[] PartsSeparators = "./".ToCharArray();

    public static (string longName, string shortName) GetDisplayNames(
        string nodeName,
        NodeType nodeType,
        bool isExecutable
    )
    {
        var name = nodeName;
        var parametersParts = "";
        var parametersIndex = name.IndexOf('(');
        if (parametersIndex > -1)
        {
            name = nodeName[..parametersIndex];
            parametersParts = nodeName[parametersIndex..];
        }
        string[] parts = name.Split(PartsSeparators);

        string namePart = parts[^1];
        string shortName = ToNiceText(namePart);

        var subParts = nodeName.StartsWith('$') ? parts : parts.Skip(1);
        string longName = string.Join(".", subParts.Where(part => !part.StartsWith('?'))) + parametersParts;

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

    static string ToNiceParameters(string fullName)
    {
        int index1 = fullName.IndexOf('(');
        int index2 = fullName.IndexOf(')');

        if (index1 <= -1 || index2 <= index1 + 1)
            return fullName;

        string parameters = fullName.Substring(index1 + 1, index2 - index1 - 1);
        string[] parametersParts = parameters.Split(",".ToCharArray());

        // Simplify parameter types to just get last part of each type
        parameters = string.Join(",", parametersParts.Select(part => part.Split(".".ToCharArray()).Last()));

        return $"{fullName.Substring(0, index1)}({parameters})";
    }

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
