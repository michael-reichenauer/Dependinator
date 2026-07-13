using Dependinator.Core.Parsing;

namespace Dependinator.UI.Modeling;

static class NodeName
{
    static readonly char[] PartsSeparators = "./".ToCharArray();

    public static (string longName, string shortName) GetDisplayNames(string nodeName, NodeType nodeType)
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

        shortName = FormatModuleSuffix(shortName);
        longName = FormatModuleSuffix(longName);

        return (longName, shortName);
    }

    // Module names are shown as "Name (dll)" instead of "Name.dll"
    static string FormatModuleSuffix(string name)
    {
        if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return $"{name[..^4]} (dll)";
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return $"{name[..^4]} (exe)";

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
