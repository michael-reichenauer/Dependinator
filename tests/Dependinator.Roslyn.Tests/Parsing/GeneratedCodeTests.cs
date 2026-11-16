using System.CodeDom.Compiler;
using System.Text.RegularExpressions;
using Dependinator.Core.Parsing;
using Dependinator.Roslyn.Parsing;
using Dependinator.Roslyn.Tests.Parsing.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dependinator.Roslyn.Tests.Parsing;

// The Regex source generator completes the partial method and adds its own types in
// System.Text.RegularExpressions.Generated, which must not show in the model.
public partial class GeneratedRegexSample
{
    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();

    public bool HasDigits(string text) => DigitsRegex().IsMatch(text);
}

[Collection(nameof(RoslynCollection))]
public class GeneratedCodeTests(RoslynFixture fixture)
{
    [Fact]
    public async Task ParseProject_ShouldLeaveOutRegexGeneratorTypes()
    {
        Assert.Contains(fixture.AllTestTypes, t =>
            t.ContainingNamespace.ToDisplayString() == "System.Text.RegularExpressions.Generated"
        );

        var items = AssertOk(await new SourceParser().ParseProjectAsync(fixture.Project, null));

        Assert.DoesNotContain(items.Nodes(), n => n.Name.Contains("RegularExpressions"));
        Assert.DoesNotContain(items.Links(), l => l.Target.Contains("RegularExpressions.Generated"));
    }

    [Fact]
    public void ParseType_ShouldKeepTheUsersGeneratedRegexMethod()
    {
        var items = TypeParser
            .ParseType(fixture.Type<GeneratedRegexSample>(), fixture.Compilation, fixture.ModelName)
            .ToList();

        var regexMethod = Assert.Single(items.Nodes(), n => n.Name.Contains("DigitsRegex"));
        Assert.Equal(Util.CurrentFilePath(), regexMethod.Properties.FileSpan!.Path);
        Assert.Contains(items.Links(), l => l.Source.Contains("HasDigits") && l.Target == regexMethod.Name);
    }

    const string UserCode = """
        namespace Sample;

        public partial class JsonContext
        {
            public int Own() => 1;

            public int CallsGenerated() => Generated();
        }

        public class User
        {
            public int Use(JsonContext context) => context.Generated() + new Helper().Value + context.Own();
        }
        """;

    // What a generator adds: a part of the user's class with its own members, and its own types
    const string GeneratorCode = """
        using System.CodeDom.Compiler;

        namespace Sample
        {
            [GeneratedCode("Sample.Generator", "1.0")]
            public partial class JsonContext
            {
                public int Generated() => Own();
            }
        }

        namespace Sample.Generated
        {
            [GeneratedCode("Sample.Generator", "1.0")]
            public class Helper
            {
                public int Value => new Nested().Get();

                public class Nested
                {
                    public int Get() => 2;
                }
            }
        }
        """;

    static Compilation CreateCompilation() =>
        CSharpCompilation.Create(
            "Sample",
            [
                CSharpSyntaxTree.ParseText(UserCode, path: "/repo/src/Sample/User.cs"),
                CSharpSyntaxTree.ParseText(GeneratorCode, path: "/repo/src/Sample/obj/Generator/Generated.g.cs"),
            ],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(GeneratedCodeAttribute).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

    static IReadOnlyList<Item> Parse(Compilation compilation)
    {
        var moduleName = Names.GetModuleName(compilation);
        return
        [
            .. Compiler.GetAllTypes(compilation).SelectMany(t => TypeParser.ParseType(t, compilation, moduleName)),
            .. NamespaceParser.ParseNamespaces(compilation, moduleName),
        ];
    }

    [Fact]
    public void ParseType_ShouldLeaveOutGeneratedTypesAndMembers()
    {
        var nodes = Parse(CreateCompilation()).Nodes().Select(n => n.Name).ToList();

        Assert.Contains("Sample.Sample.JsonContext", nodes);
        Assert.Contains("Sample.Sample.JsonContext.Own()", nodes);
        Assert.Contains("Sample.Sample.User.Use(Sample.JsonContext)", nodes);
        Assert.DoesNotContain(nodes, n => n.Contains(".Generated") || n.Contains("Helper"));
    }

    [Fact]
    public void ParseType_ShouldNotLinkToGeneratedCode()
    {
        var links = Parse(CreateCompilation()).Links();

        Assert.DoesNotContain(links, l => l.Target.Contains(".Generated") || l.Target.Contains("Helper"));
        Assert.DoesNotContain(links, l => l.Source.Contains(".Generated") || l.Source.Contains("Helper"));

        // A use of a generated member of the user's type is a use of the type
        var fromUse = links.Where(l => l.Source.EndsWith(".Sample.User.Use(Sample.JsonContext)")).ToList();
        Assert.Contains(fromUse, l => l.Target.EndsWith(".Sample.JsonContext"));
        Assert.Contains(fromUse, l => l.Target.EndsWith(".Sample.JsonContext.Own()"));

        // ... but not a link from the type's own member to the type
        Assert.DoesNotContain(links, l => l.Source.Contains("CallsGenerated"));
    }
}
