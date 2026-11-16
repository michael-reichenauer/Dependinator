using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

// Code a source generator wrote, such as the System.Text.RegularExpressions.Generated types of
// [GeneratedRegex] or the Functions worker's executor, is left out of the model: nobody wrote it
// and nobody can change it, so it only adds nodes and lines. A type or member is generated when
// every one of its declarations is in a source generator's output and carries [GeneratedCode] or
// [CompilerGenerated] (or is inside such a declaration of a type around it).
// - Checked-in or build-step generated code (resx Resources, NSwag or gRPC clients) is in the
//   project's own documents, not a generator's output: the user depends on it, so it stays.
// - A user's partial class or partial method that a generator completes (a JSON context, a
//   [GeneratedRegex] method) has a declaration of the user's own, so it stays; what the
//   generator adds inside its part of the class goes.
// - Razor components are a generator's output too, but carry neither attribute, so they stay.
static class GeneratedCode
{
    static readonly ConditionalWeakTable<ISymbol, StrongBox<bool>> cache = new();
    static readonly ConditionalWeakTable<SyntaxTree, object> generatorOutput = new();

    // Marks the compilation's syntax trees that are not the project's own documents, that is,
    // the source generators' output.
    public static void MarkGeneratorOutput(Compilation compilation, IEnumerable<string?> documentPaths)
    {
        var paths = documentPaths.OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees.Where(t => !paths.Contains(t.FilePath)))
            MarkGeneratorOutput(tree);
    }

    public static void MarkGeneratorOutput(SyntaxTree tree) => generatorOutput.AddOrUpdate(tree, tree);

    public static bool IsGeneratorOutput(SyntaxTree tree) => generatorOutput.TryGetValue(tree, out _);

    public static bool IsGenerated(ISymbol symbol)
    {
        // A partial member is one member: the generator's part and the user's part count as one
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
        var hasTypes = false;
        foreach (var type in Compiler.GetAllNamedTypes(ns).Where(t => !t.IsImplicitlyDeclared))
        {
            if (!IsGenerated(type))
                return false;
            hasTypes = true;
        }
        return hasTypes;
    }

    static bool IsGeneratedSymbol(ISymbol symbol)
    {
        if (symbol.ContainingType is { } containingType && IsGenerated(containingType))
            return true;

        var parts = PartsOf(symbol);
        var declarations = parts.SelectMany(p => p.DeclaringSyntaxReferences).ToList();
        if (!declarations.All(d => IsGeneratorOutput(d.SyntaxTree)))
            return false;

        // The declarations that carry the attributes, of the symbol or of a type around it: a
        // member the generator adds to the user's partial class is inside its attributed part
        var attributedDeclarations = parts
            .SelectMany(SymbolAndContainingTypes)
            .SelectMany(s => s.GetAttributes())
            .Where(IsGeneratedAttribute)
            .Select(a => a.ApplicationSyntaxReference?.GetSyntax().Parent?.Parent)
            .OfType<SyntaxNode>()
            .ToHashSet();
        if (attributedDeclarations.Count == 0)
            return false;

        // A field's declaration is its variable declarator, inside the attributed field declaration
        return declarations.All(reference =>
            reference.GetSyntax().AncestorsAndSelf().Any(attributedDeclarations.Contains)
        );
    }

    // The definition and the implementation of a partial member, whichever of them the user
    // wrote (a [GeneratedRegex] method's definition, a generated partial hook's implementation)
    static IReadOnlyList<ISymbol> PartsOf(ISymbol symbol) =>
        symbol switch
        {
            IMethodSymbol { PartialImplementationPart: { } implementation } => [symbol, implementation],
            IPropertySymbol { PartialImplementationPart: { } implementation } => [symbol, implementation],
            _ => [symbol],
        };

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
