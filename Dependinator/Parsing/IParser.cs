using System.Threading.Channels;
using Dependinator.Models;

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

record Link(string SourceName, string TargetName, NodeType TargetType) : IItem;

record Node(string Name, string ParentName, NodeType Type, string? Description) : IItem
{
    static readonly char[] NamePartsSeparators = "./".ToCharArray();

    public static string ParseParentName(string name)
    {
        int index = name.LastIndexOfAny(NamePartsSeparators);
        return index > -1 ? name[..index] : "";
    }
}

record Source(string Path, string Text, int LineNumber);

record NodeType(string Text)
{
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
    public static readonly NodeType Private = new("Private");

    public override string ToString() => Text;
}
