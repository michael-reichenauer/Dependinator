using Microsoft.CodeAnalysis;

namespace DependinatorCore.Parsing.Sources.Roslyn;

static class NodeTypes
{
    public static (NodeType, MemberType?) ToTypes(ISymbol member) =>
        member switch
        {
            INamedTypeSymbol t => (NodeType.Type, null),
            IMethodSymbol => (NodeType.Member, MemberType.Method),
            IPropertySymbol => (NodeType.Member, MemberType.Property),
            IFieldSymbol => (NodeType.Member, MemberType.Field),
            IEventSymbol => (NodeType.Member, MemberType.Event),
            _ => (NodeType.None, null),
        };
}
