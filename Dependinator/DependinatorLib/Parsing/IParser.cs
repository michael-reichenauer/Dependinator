using System.Threading.Channels;

namespace Dependinator.Parsing;


interface IParser
{
    bool CanSupport(string path);

    Task<R> ParseAsync(string path, ChannelWriter<IItem> items);

    Task<R<Source>> GetSourceAsync(string path, string nodeName);

    Task<R<string>> GetNodeAsync(string path, Source source);

    DateTime GetDataTime(string path);
}

interface IItem { }
record Link(string Source, string Target, NodeType TargetType) : IItem;

record Node(string Name, string ParentName, NodeType Type, string Description) : IItem
{
    static readonly char[] NamePartsSeparators = "./".ToCharArray();

    static public string ParseParentName(string name)
    {
        int index = name.LastIndexOfAny(NamePartsSeparators);
        return index > -1 ? name[..index] : "";
    }
}


record Source(string Path, string Text, int LineNumber);


internal enum NodeType
{
    None,
    Root,
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
    PrivateMember
}

static class NodeTypeEx
{
    public static string ToText(NodeType type) => Enum.GetName(type) ?? "None";

    public static NodeType ToNodeType(string text) => Enum.TryParse<NodeType>(text, out var type) ? type : NodeType.None;
}



// static class NodeType
// {
//     public const string SolutionType = "Solution";
//     public const string AssemblyType = "Assembly";
//     public const string Externals = "Externals";
//     public const string GroupType = "Group";
//     public const string DllType = "Dll";
//     public const string ExeType = "Exe";
//     public const string NamespaceType = "Namespace";
//     public const string TypeType = "Type";
//     public const string MemberType = "Member";
//     public const string SolutionFolderType = "SolutionFolder";
// }

