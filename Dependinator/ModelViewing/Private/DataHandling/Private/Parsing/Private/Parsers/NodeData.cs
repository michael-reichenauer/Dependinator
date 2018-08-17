namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers
{
    public class NodeData
    {
        public string Name { get; }
        public string Parent { get; }
        public string Type { get; }
        public string Description { get; }


        public NodeData(string name, string parent, string type, string description)
        {
            Name = name;
            Parent = parent;
            Type = type;
            Description = description;
        }


        public override string ToString() => Name;
        

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
}
