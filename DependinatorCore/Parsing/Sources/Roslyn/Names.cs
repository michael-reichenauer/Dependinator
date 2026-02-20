using Microsoft.CodeAnalysis;

namespace DependinatorCore.Parsing.Sources;

static class Names
{
    static SymbolDisplayFormat MemberFormat = new(
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

    public static string GetFullTypeName(ISymbol typeSymbol, string moduleName)
    {
        var fqTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var fullName = fqTypeName.TrimPrefix("global::").Replace("&", "").Replace(" ", "");
        return $"{moduleName}.{fullName}";
    }

    public static string GetFullMemberName(ISymbol typeSymbol, string typeName)
    {
        var memberName = typeSymbol.ToDisplayString(MemberFormat);
        return $"{typeName}.{memberName}";
    }
}
