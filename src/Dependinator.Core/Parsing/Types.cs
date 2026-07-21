namespace Dependinator.Core.Parsing;

record Item(Node? Node, Link? Link, LineDescription? LineDescription = null);

record Link(string Source, string Target, LinkProperties Properties);

record LineDescription(string Source, string Target, string Text);

record Node(string Name, NodeProperties Properties);

class NodeProperties
{
    public NodeType? Type { get; init; }
    public string? Description { get; init; }
    public string? Parent { get; init; }
    public bool? IsPrivate { get; init; }

    // For assembly nodes: the project builds an executable (OutputType Exe). SDK-built
    // executables still compile to a ".dll" module (the ".exe" is just the apphost), so
    // this cannot be derived from the module name.
    public bool? IsExecutable { get; init; }
    public FileSpan? FileSpan { get; init; }
}

class LinkProperties
{
    public string? Description { get; init; }
    public NodeType? TargetType { get; init; }
    public bool? IsInheritance { get; init; }
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

    // Type is the generic "a type" value (used for link targets and unknown/external types).
    // The ClassType..RecordType values distinguish a parsed type's kind for icon selection,
    // in the same way the *Member values distinguish member kinds.
    Type,
    ClassType,
    InterfaceType,
    EnumType,
    StructType,
    RecordType,
    FieldMember,
    ConstructorMember,
    EventMember,
    PropertyMember,
    MethodMember,
}

static class NodeDescriptions
{
    public const string Externals =
        "External references used by, but not part of, this solution (e.g. third-party packages and framework assemblies).";
}

static class NoValue
{
    public static readonly int Int = int.MinValue;
    public static readonly string String = "\u0000";
    public static readonly FileSpan FileSpan = new(NoValue.String, NoValue.Int, NoValue.Int);
}
