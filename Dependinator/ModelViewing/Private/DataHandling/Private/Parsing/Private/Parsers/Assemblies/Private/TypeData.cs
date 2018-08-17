using Mono.Cecil;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Assemblies.Private
{
    internal class TypeData
    {
        public TypeData(TypeDefinition type, NodeData node, bool isAsyncStateType)
        {
            Type = type;
            Node = node;
            IsAsyncStateType = isAsyncStateType;
        }


        public TypeDefinition Type { get; }
        public NodeData Node { get; }
        public bool IsAsyncStateType { get; }
    }
}
