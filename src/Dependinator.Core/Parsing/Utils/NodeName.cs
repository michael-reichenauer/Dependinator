namespace Dependinator.Core.Parsing.Utils;

internal class NodeName : IEquatable<NodeName>
{
    record DisplayParts(string ShortName, string LongName);

    public static NodeName Root = From("");
    readonly Lazy<DisplayParts> displayParts;

    readonly Lazy<NodeName> parentName;

    NodeName(string fullName)
    {
        FullName = fullName;

        parentName = new Lazy<NodeName>(GetParentName);
        displayParts = new Lazy<DisplayParts>(GetDisplayParts);
    }

    public string FullName { get; }
    public NodeName ParentName => parentName.Value;
    public string DisplayShortName => displayParts.Value.ShortName;
    public string DisplayLongName => displayParts.Value.LongName;

    public static NodeName From(string fullName)
    {
        return new NodeName(fullName);
    }

    public static string ParseParentName(string name)
    {
        var separators = GetPartSeparators(name);
        return separators.Count > 0 ? name[..separators[^1]] : "";
    }

    // The name's parts, e.g. ["Mod", "Ns", "Type<A.B>", "Do(A.B)"] for "Mod.Ns.Type<A.B>.Do(A.B)"
    public static IReadOnlyList<string> SplitParts(string name)
    {
        List<string> parts = [];
        var start = 0;
        foreach (var separator in GetPartSeparators(name))
        {
            parts.Add(name[start..separator]);
            start = separator + 1;
        }
        parts.Add(name[start..]);
        return parts;
    }

    // The indexes of the '.' and '/' part separators. Those inside brackets belong to a type name
    // within the part: a generic argument (Ns.Type<A.B>.Member), a parameter (Ns.Type.Do(A.B)),
    // an indexer parameter (Ns.Type.this[A.B].get) or a C# extension block's receiver
    // (Ns.Extensions.extension(A.B).Member, a member inside its block).
    static List<int> GetPartSeparators(string name)
    {
        List<int> separators = [];
        int depth = 0;
        for (int i = 0; i < name.Length; i++)
        {
            switch (name[i])
            {
                case '<' or '(' or '[':
                    depth++;
                    break;
                case '>' or ')' or ']':
                    // Never below 0: the '>' of an operator (operator>) closes no bracket
                    depth = Math.Max(0, depth - 1);
                    break;
                case '.' or '/' when depth == 0:
                    separators.Add(i);
                    break;
            }
        }

        return separators;
    }

    public bool IsSame(string nameText) => nameText == FullName;

    public bool Equals(NodeName? other) => other is not null && FullName == other.FullName;

    public override bool Equals(object? obj) => obj is NodeName other && Equals(other);

    public override int GetHashCode() => FullName.GetHashCode();

    public static bool operator ==(NodeName? left, NodeName? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(NodeName? left, NodeName? right) => !(left == right);

    public override string ToString() => this != Root ? FullName : "<root>";

    // public static implicit operator NodeName(DataNodeName dataName) => new NodeName((string)dataName);
    // public static implicit operator DataNodeName(NodeName nodeName) => (DataNodeName)nodeName.FullName;

    NodeName GetParentName()
    {
        var parentName = ParseParentName(FullName);
        return parentName != "" ? From(parentName) : Root;
    }

    DisplayParts GetDisplayParts()
    {
        string name = "";

        var parts = SplitParts(FullName);

        string namePart = parts[^1];
        int index = namePart.IndexOf('(');
        if (index > -1)
        {
            name = namePart.Substring(0, index);
        }
        else
        {
            name = namePart;
        }

        name = ToNiceText(name);

        var subParts = FullName.StartsWith("$") ? parts : parts.Skip(1);
        string fullName = string.Join(".", subParts.Where(part => !part.StartsWith("?")));

        if (string.IsNullOrEmpty(fullName))
        {
            fullName = ToNiceText(FullName);
        }
        else
        {
            fullName = ToNiceText(fullName);
            fullName = ToNiceParameters(fullName);
        }

        return new DisplayParts(name, fullName);
    }

    static string ToNiceParameters(string fullName)
    {
        int index1 = fullName.IndexOf('(');
        int index2 = fullName.IndexOf(')');

        if (index1 > -1 && index2 > index1 + 1)
        {
            string parameters = fullName.Substring(index1 + 1, index2 - index1 - 1);
            string[] parametersParts = parameters.Split(",".ToCharArray());

            // Simplify parameter types to just get last part of each type
            parameters = string.Join(",", parametersParts.Select(part => part.Split(".".ToCharArray()).Last()));

            fullName = $"{fullName.Substring(0, index1)}({parameters})";
        }

        return fullName;
    }

    static string ToNiceText(string name)
    {
        return name.Replace("*", ".")
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
}
