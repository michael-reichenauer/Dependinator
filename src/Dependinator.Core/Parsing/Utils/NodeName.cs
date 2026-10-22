using System.Buffers;

namespace Dependinator.Core.Parsing.Utils;

internal class NodeName : IEquatable<NodeName>
{
    public static NodeName Root = From("");
    static readonly char[] PartSeparators = ['.', '/'];
    static readonly SearchValues<char> Brackets = SearchValues.Create("<([");

    readonly Lazy<NodeName> parentName;

    NodeName(string fullName)
    {
        FullName = fullName;

        parentName = new Lazy<NodeName>(GetParentName);
    }

    public string FullName { get; }
    public NodeName ParentName => parentName.Value;

    public static NodeName From(string fullName)
    {
        return new NodeName(fullName);
    }

    public static string ParseParentName(string name)
    {
        int index = name.AsSpan().ContainsAny(Brackets)
            ? GetPartSeparators(name).LastOrDefault(-1)
            : name.LastIndexOfAny(PartSeparators);
        return index > -1 ? name[..index] : "";
    }

    // The name's parts, e.g. ["Mod", "Ns", "Type<A.B>", "Do(A.B)"] for "Mod.Ns.Type<A.B>.Do(A.B)"
    public static IReadOnlyList<string> SplitParts(string name)
    {
        if (!name.AsSpan().ContainsAny(Brackets))
            return name.Split(PartSeparators);

        List<string> parts = [];
        int start = 0;
        foreach (int separator in GetPartSeparators(name))
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
    // (Ns.Extensions.extension(A.B).Member, a member inside its block). A bracket that is never
    // closed, or closes none, is no bracket: the '<' or '>' of an operator (Ns.Type.operator<(A.B))
    // or one in a manual node's name ("Latency < 50ms.Child").
    static List<int> GetPartSeparators(string name)
    {
        List<int> separators = [];
        // The brackets still open: the bracket that closes each, and the separators before it
        List<(char Close, int SeparatorCount)> open = [];
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            switch (c)
            {
                case '.' or '/':
                    separators.Add(i);
                    break;
                case '<':
                    open.Add(('>', separators.Count));
                    break;
                case '(':
                    open.Add((')', separators.Count));
                    break;
                case '[':
                    open.Add((']', separators.Count));
                    break;
                case '>' or ')' or ']':
                    int opening = open.Count - 1;
                    while (opening >= 0 && open[opening].Close != c)
                        opening--;
                    if (opening < 0)
                        break;

                    // The separators since it opened are inside it, also those of brackets opened
                    // inside it and never closed
                    int separatorCount = open[opening].SeparatorCount;
                    separators.RemoveRange(separatorCount, separators.Count - separatorCount);
                    open.RemoveRange(opening, open.Count - opening);
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
}
