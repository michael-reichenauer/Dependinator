using Microsoft.CodeAnalysis;

namespace DependinatorCore.Parsing.Sources.Roslyn;

static class Names
{
    static readonly SymbolDisplayFormat MemberFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
    );

    public static string GetModuleName(Compilation compilation)
    {
        // return compilation.AssemblyName?.Replace(".", "*")!;
        var name = compilation.Options.ModuleName ?? compilation.AssemblyName;
        return name?.Replace(".", "*") ?? "global::";
    }

    public static string GetFullTypeName(INamedTypeSymbol typeSymbol, string moduleName)
    {
        var fqTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var fullName = fqTypeName.TrimPrefix("global::").Replace("&", "").Replace(" ", "");
        return $"{moduleName}.{fullName}";
    }

    public static string GetFullMemberName(ISymbol memberSymbol, string fullTypeName)
    {
        var memberName = memberSymbol.ToDisplayString(MemberFormat);
        return $"{fullTypeName}.{memberName}";
    }
}
