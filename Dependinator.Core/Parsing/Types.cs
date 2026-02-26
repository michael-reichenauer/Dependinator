namespace Dependinator.Core.Parsing;

record Item(Node? Node, Link? Link);

record Link(string Source, string Target, LinkProperties Properties);

record Node(string Name, NodeProperties Properties);

class NodeProperties
{
    public NodeType? Type { get; init; }
    public string? Description { get; init; }
    public string? Parent { get; init; }
    public bool? IsPrivate { get; init; }
    public FileSpan? FileSpan { get; init; }
}

class LinkProperties
{
    public string? Description { get; init; }
    public NodeType? TargetType { get; init; }
}

record Source(string Text, FileLocation Location);

record FileLocation(string Path, int Line);

record FileSpan(string Path, int StartLine, int EndLine);

enum NodeType
{
    None,
    Root,
    Parent,
    Solution,
    Externals,
    SolutionFolder,
    Assembly,
    Group,
    Dll,
    Exe,
    Namespace,
    Type,
    FieldMember,
    ConstructorMember,
    EventMember,
    PropertyMember,
    MethodMember,
}

static class NoValue
{
    public static readonly int Int = int.MinValue;
    public static readonly string String = "\u0000";
    public static readonly FileSpan FileSpan = new(NoValue.String, NoValue.Int, NoValue.Int);
}
