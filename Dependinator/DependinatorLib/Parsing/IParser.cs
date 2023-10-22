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
record Link(string Source, string Target, string TargetType) : IItem;
record Node(string Name, string Parent, string Type, string Description) : IItem
{
    public static Node Default(string name) => new Node(name, "", "", "");
}

record Source(string Path, string Text, int LineNumber);

static class NodeType
{
    public const string SolutionType = "Solution";
    public const string AssemblyType = "Assembly";
    public const string GroupType = "Group";
    public const string DllType = "Dll";
    public const string ExeType = "Exe";
    public const string NameSpaceType = "NameSpace";
    public const string TypeType = "Type";
    public const string MemberType = "Member";
    public const string SolutionFolderType = "SolutionFolder";
}

