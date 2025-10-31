using System.Reflection;

namespace Dependinator.Parsing;

enum MemberType
{
    None,
    Field,
    Event,
    Property,
    Method,
    Constructor,
}

interface IItem { }

record Link(string Source, string Target, LinkAttributes Attributes) : IItem;

record Node(string Name, NodeAttributes Attributes) : IItem;

class NodeAttributes
{
    public string Description { get; init; } = "";
    public NodeType Type { get; init; } = NodeType.None;
    public string Parent { get; init; } = "";
    public bool IsPrivate { get; init; }
    public MemberType MemberType { get; init; }
}

class LinkAttributes
{
    public string Description { get; init; } = "";
    public NodeType TargetType { get; init; } = NodeType.None;
}

record Source(string Path, string Text, int LineNumber);

record NodeType
{
    public string Text { get; init; }

    private NodeType(string text) => Text = text;

    public static readonly NodeType None = new("None");
    public static readonly NodeType Root = new("Root");
    public static readonly NodeType Parent = new("Parent");
    public static readonly NodeType Solution = new("Solution");
    public static readonly NodeType Externals = new("Externals");
    public static readonly NodeType SolutionFolder = new("SolutionFolder");
    public static readonly NodeType Assembly = new("Assembly");
    public static readonly NodeType Group = new("Group");
    public static readonly NodeType Dll = new("Dll");
    public static readonly NodeType Exe = new("Exe");
    public static readonly NodeType Namespace = new("Namespace");
    public static readonly NodeType Type = new("Type");
    public static readonly NodeType Member = new("Member");

    public static implicit operator NodeType(string typeName) =>
        All.FirstOrDefault(m => m.Text == typeName)
        ?? throw new NotSupportedException($"Node type '{typeName}' not supported");

    public static IReadOnlyList<NodeType> All { get; } =
    [
        .. typeof(NodeType)
            .GetMembers(BindingFlags.Public | BindingFlags.Static)
            .Select(m =>
                m switch
                {
                    FieldInfo f when f.FieldType == typeof(NodeType) => f.GetValue(null) as NodeType,
                    _ => null,
                }
            )
            .Where(x => x is not null)
            .Cast<NodeType>(),
    ];

    public override string ToString() => Text;
}
