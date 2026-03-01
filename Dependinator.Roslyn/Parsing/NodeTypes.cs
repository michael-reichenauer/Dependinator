using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

static class NodeTypes
{
    public static NodeType ToTypes(ISymbol member) =>
        member switch
        {
            INamedTypeSymbol t => NodeType.Type,
            IMethodSymbol => NodeType.MethodMember,
            IPropertySymbol => NodeType.PropertyMember,
            IFieldSymbol => NodeType.FieldMember,
            IEventSymbol => NodeType.EventMember,
            _ => NodeType.None,
        };
}
