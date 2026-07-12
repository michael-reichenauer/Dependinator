using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

static class SymbolUtils
{
    public static bool GetIsPrivate(ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Private;
    }

    // Maps a Roslyn type symbol to the specific type NodeType used for icon selection. Records
    // (both class and struct records) are reported as RecordType; anything not interface/enum/struct
    // falls back to ClassType.
    public static NodeType GetTypeNodeType(INamedTypeSymbol type) =>
        type switch
        {
            { TypeKind: TypeKind.Interface } => NodeType.InterfaceType,
            { TypeKind: TypeKind.Enum } => NodeType.EnumType,
            { IsRecord: true } => NodeType.RecordType,
            { TypeKind: TypeKind.Struct } => NodeType.StructType,
            _ => NodeType.ClassType,
        };
}
