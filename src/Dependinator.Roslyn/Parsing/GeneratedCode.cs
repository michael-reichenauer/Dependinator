using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

// Code a source generator wrote, such as the System.Text.RegularExpressions.Generated types of
// [GeneratedRegex] or the Functions worker's executor, is left out of the model: nobody wrote it
// and nobody can change it, so it only adds nodes and lines. A type or member is generated when
// every one of its declarations carries [GeneratedCode] or [CompilerGenerated] (or it is inside
// a generated type). The attributes of a partial type or method are merged from all its parts,
// so a user's partial class or [GeneratedRegex] method that a generator completes still has a
// declaration of its own without them, and stays; what the generator adds inside its part of the
// class goes. Razor components are generated trees too, but without these attributes, so they
// stay as well.
static class GeneratedCode
{
    static readonly ConditionalWeakTable<ISymbol, StrongBox<bool>> cache = new();

    public static bool IsGenerated(ISymbol symbol)
    {
        // The generator's implementation of a partial member is the user's declared member
        symbol = symbol switch
        {
            IMethodSymbol { PartialDefinitionPart: { } definition } => definition,
            IPropertySymbol { PartialDefinitionPart: { } definition } => definition,
            _ => symbol,
        };

        // Metadata symbols (referenced assemblies) have no declarations of their own here
        if (symbol.DeclaringSyntaxReferences.IsEmpty)
            return false;

        return cache.GetValue(symbol, s => new StrongBox<bool>(IsGeneratedSymbol(s))).Value;
    }

    // A namespace that holds only generated types (System.Text.RegularExpressions.Generated)
    public static bool IsGeneratedOnly(INamespaceSymbol ns)
    {
        var types = Compiler.GetAllTypes(ns).Where(t => !t.IsImplicitlyDeclared).ToList();
        return types.Count > 0 && types.All(IsGenerated);
    }

    static bool IsGeneratedSymbol(ISymbol symbol)
    {
        if (symbol.ContainingType is { } containingType && IsGenerated(containingType))
            return true;

        // The declarations that carry the attributes, of the symbol or of a type around it: a
        // member the generator adds to the user's partial class is inside its attributed part
        var attributedDeclarations = SymbolAndContainingTypes(symbol)
            .SelectMany(s => s.GetAttributes())
            .Where(IsGeneratedAttribute)
            .Select(a => a.ApplicationSyntaxReference?.GetSyntax().Parent?.Parent)
            .OfType<SyntaxNode>()
            .ToHashSet();
        if (attributedDeclarations.Count == 0)
            return false;

        // A field's declaration is its variable declarator, inside the attributed field declaration
        return symbol.DeclaringSyntaxReferences.All(reference =>
            reference.GetSyntax().AncestorsAndSelf().Any(attributedDeclarations.Contains)
        );
    }

    static IEnumerable<ISymbol> SymbolAndContainingTypes(ISymbol symbol)
    {
        for (ISymbol? s = symbol; s is not null; s = s.ContainingType)
            yield return s;
    }

    static bool IsGeneratedAttribute(AttributeData attribute) =>
        attribute.AttributeClass?.ToDisplayString()
            is "System.CodeDom.Compiler.GeneratedCodeAttribute"
                or "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
}
