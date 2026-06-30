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
    public static NodeType GetTypeNodeType(INamedTypeSymbol type)
    {
        if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface)
            return NodeType.InterfaceType;
        if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
            return NodeType.EnumType;
        if (type.IsRecord)
            return NodeType.RecordType;
        if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct)
            return NodeType.StructType;
        return NodeType.ClassType;
    }
}
