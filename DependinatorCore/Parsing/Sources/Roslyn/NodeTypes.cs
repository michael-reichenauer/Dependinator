using Microsoft.CodeAnalysis;

namespace DependinatorCore.Parsing.Sources.Roslyn;

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
