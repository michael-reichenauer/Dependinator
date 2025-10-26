using Dependinator.Parsing;

namespace Dependinator.Models;

class NodeName
{
    static readonly char[] PartsSeparators = "./".ToCharArray();

    public static (string longName, string shortName) GetDisplayNames(string nodeName, NodeType nodeType)
    {
        if (nodeName.Contains("LowestCommonAncestor"))
        {
            var a = 0;
        }
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

        shortName = nodeType == NodeType.Assembly && !shortName.EndsWith(".dll") ? shortName + ".dll" : shortName;
        longName = nodeType == NodeType.Assembly && !shortName.EndsWith(".dll") ? longName + ".dll" : longName;

        return (longName, shortName);
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
            .Replace("`1", "<T>")
            .Replace("`2", "<T,T>")
            .Replace("`3", "<T,T,T>")
            .Replace("`4", "<T,T,T,T>")
            .Replace("`5", "<T,T,T,T,T>")
            .Replace("op_Equality", "==")
            .Replace("op_Inequality", "!=");
}
