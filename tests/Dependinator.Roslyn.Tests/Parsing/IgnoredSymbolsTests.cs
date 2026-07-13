using Dependinator.Core.Parsing;
using Dependinator.Roslyn.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dependinator.Roslyn.Tests.Parsing;

public class AnonymousTypeUser
{
    // Anonymous types (and their property accesses) must not produce links/nodes
    public string UseAnonymousType()
    {
        var message = new { type = "vscode/ShowEditor", message = "payload" };
        return message.type + message.message;
    }
}

[Collection(nameof(RoslynCollection))]
public class IgnoredSymbolsTests(RoslynFixture fixture)
{
    [Fact]
    public void ParseType_ShouldNotLinkToAnonymousTypeMembers()
    {
        var items = TypeParser
            .ParseType(fixture.Type<AnonymousTypeUser>(), fixture.Compilation, fixture.ModelName)
            .ToList();

        Assert.DoesNotContain(
            items,
            item => item.Link is { } link && link.Target.Contains("anonymous", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void ParseType_ShouldNotLinkToUnresolvedTypes()
    {
        // "Undefined" does not resolve, so the inferred type of "var" is an error type,
        // which previously produced bogus link targets like "Sample.var" or "Sample."
        var syntaxTree = CSharpSyntaxTree.ParseText(
            "class Sample { void Method() { var value = Undefined.Get(); } }",
            path: "/repo/src/Sample/Sample.cs"
        );
        var compilation = CSharpCompilation.Create(
            "Sample",
            [syntaxTree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]
        );
        var type = (INamedTypeSymbol)compilation.GetSymbolsWithName("Sample").Single();

        var items = TypeParser.ParseType(type, compilation, "Sample").ToList();

        Assert.DoesNotContain(
            items,
            item => item.Link is { } link && (link.Target.EndsWith(".") || link.Target.EndsWith(".var"))
        );
    }

    [Fact]
    public void ParseNamespaces_ShouldIgnoreCompilerGeneratedNamespaces()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            """
            namespace __Blazor.Sample.Components
            {
                class BlazorGenerated { }
            }

            namespace Microsoft.CodeAnalysis
            {
                class EmbeddedAttribute { }
            }

            namespace Sample.Real
            {
                class RealType { }
            }
            """,
            path: "/repo/src/Sample/Generated.cs"
        );
        var compilation = CSharpCompilation.Create(
            "Sample",
            [syntaxTree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]
        );

        List<Item> items = NamespaceParser.ParseNamespaces(compilation, "Sample").ToList();

        Assert.DoesNotContain(
            items,
            item => item.Node is { } node && (node.Name.Contains("__Blazor") || node.Name.Contains("Microsoft"))
        );
        Assert.Contains(items, item => item.Node is { } node && node.Name == "Sample.Sample.Real");
    }
}
