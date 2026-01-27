namespace DependinatorCore.Parsing;

enum MemberType
{
    None,
    Field,
    Event,
    Property,
    Method,
    Constructor,
}

record Item(Node? Node, Link? Link);

record Link(string Source, string Target, LinkAttributes Attributes);

record Node(string Name, NodeAttributes Attributes);

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

record SourceLocation(string Path, int LineNumber);

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
    Member,
}
