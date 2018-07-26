using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Mono.Cecil;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.AssemblyParsing.Private
{
    internal class TypeData
    {
        public TypeData(TypeDefinition type, DataNode node, bool isAsyncStateType)
        {
            Type = type;
            Node = node;
            IsAsyncStateType = isAsyncStateType;
        }


        public TypeDefinition Type { get; }
        public DataNode Node { get; }
        public bool IsAsyncStateType { get; }
    }
}
