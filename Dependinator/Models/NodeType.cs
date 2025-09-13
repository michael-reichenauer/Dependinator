using System.Reflection;

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

    public static implicit operator NodeType(Parsing.NodeType type) => new(type.Text);

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
                    PropertyInfo p when p.PropertyType == typeof(NodeType) => p.GetValue(null) as NodeType,
                    _ => null,
                }
            )
            .Where(x => x is not null)
            .Cast<NodeType>(),
    ];

    public string IconName =>
        Text switch
        {
            "Solution" => "SolutionIcon",
            "Externals" => "ExternalsIcon",
            "Assembly" => "ModuleIcon",
            "Namespace" => "FilesIcon",
            "Private" => "PrivateIcon",
            "Parent" => "FilesIcon",
            "Type" => "TypeIcon",
            "Member" => "MemberIcon",
            _ => "ModuleIcon",
        };

    public override string ToString() => Text;
}
