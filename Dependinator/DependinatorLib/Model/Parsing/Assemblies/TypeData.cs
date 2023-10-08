using Mono.Cecil;
using Dependinator.Model.Parsing;


namespace Dependinator.Model.Parsers.Assemblies;

internal class TypeData
{
    public TypeData(TypeDefinition type, Node node, bool isAsyncStateType)
    {
        Type = type;
        Node = node;
        IsAsyncStateType = isAsyncStateType;
    }


    public TypeDefinition Type { get; }
    public Node Node { get; }
    public bool IsAsyncStateType { get; }
}

