namespace Dependinator.Model.Parsing;


interface IParser
{
    bool CanSupport(string path);

    Task<R> ParseAsync(string path, Action<Node> nodeCallback, Action<Link> linkCallback);

    Task<R<Source>> GetSourceAsync(string path, string nodeName);

    Task<R<string>> GetNodeAsync(string path, Source source);

    DateTime GetDataTime(string path);
}

interface IItems { }


record Link(string Source, string Target, string TargetType) : IItems
{
    public override string ToString() => $"{Source}->{Target}";
}


record Node(string Name, string Parent, string Type, string Description) : IItems
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

    public override string ToString() => Name;
}

record Source(string Path, string Text, int LineNumber);
