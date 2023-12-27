namespace Dependinator.Models;


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
    public static readonly NodeType Private = new("Private");

    public static implicit operator NodeType(Parsing.NodeType type) => new(type.ToString());

    public string IconName => Text switch
    {
        "Solution" => "SolutionIcon",
        "Externals" => "ExternalsIcon",
        "Assembly" => "ModuleIcon",
        "Namespace" => "FilesIcon",
        "Private" => "PrivateIcon",
        "Parent" => "FilesIcon",
        "Type" => "TypeIcon",
        "Member" => "MemberIcon",
        _ => "ModuleIcon"
    };

    public override string ToString() => Text;
}

