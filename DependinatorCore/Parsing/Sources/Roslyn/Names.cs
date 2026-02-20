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
        var name = compilation.Options.ModuleName ?? compilation.AssemblyName;
        return SanitizeModuleName(name);
    }

    public static string GetModuleName(ISymbol typeSymbol)
    {
        var name = typeSymbol.ContainingModule?.Name ?? typeSymbol.ContainingAssembly?.Name;
        return SanitizeModuleName(name);
    }

    public static string GetFullTypeName(INamedTypeSymbol typeSymbol, string moduleName)
    {
        var fqTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return ToFullTypeName(moduleName, fqTypeName);
    }

    public static string GetFullTypeName(INamedTypeSymbol typeSymbol)
    {
        var moduleName = GetModuleName(typeSymbol);
        return GetFullTypeName(typeSymbol, moduleName);
    }

    public static string GetFullMemberName(ISymbol symbol, string fullTypeName)
    {
        var memberName = symbol.ToDisplayString(MemberFormat);
        return $"{fullTypeName}.{memberName}";
    }

    public static string GetFullName(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol typeSymbol)
            return GetFullTypeName(typeSymbol);

        INamedTypeSymbol? declaringType = symbol.ContainingType;
        if (declaringType is null)
            throw new NotSupportedException($"Symbol not support {symbol}");

        var fullTypeName = GetFullTypeName(declaringType);
        return GetFullMemberName(symbol, fullTypeName);
    }

    public static string GetFullName<T>() => GetFullName(typeof(T));

    static string GetFullName(Type type)
    {
        var moduleName = SanitizeModuleName(type.Module.Name);
        return ToFullTypeName(moduleName, type.FullName!);
    }

    static string SanitizeModuleName(string? moduleName)
    {
        return moduleName?.Replace(".", "*") ?? "global::";
    }

    static string ToFullTypeName(string moduleName, string fullName)
    {
        fullName = fullName.TrimPrefix("global::").Replace("&", "").Replace(" ", "");
        return $"{moduleName}.{fullName}";
    }
}
