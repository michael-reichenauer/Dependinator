using Dependinator.Core.Parsing;
using Dependinator.TypeScriptParser.Tests.Parsing.Utils;

namespace Dependinator.TypeScriptParser.Tests.Parsing;

// Parses this repository's VS Code extension (src/Dependinator.VsCodeExt): a real project, parsed
// as it is checked out, with or without its packages installed.
public class ExtensionProjectTests
{
    [Fact]
    public async Task ParseAsync_ShouldParseTheVsCodeExtension()
    {
        var items = AssertOk(
            await TypeScriptFixture.Parser().ParseAsync(Root.ExtensionConfigPath, SolutionParseOptions.Default)
        );

        // Named after its folder, like the model, not after the package ("dependinator")
        Assert.Equal(NodeType.Solution, items.Node("Dependinator*VsCodeExt").Properties.Type);
        var activate = items.Node("Dependinator*VsCodeExt.src.extension*ts.activate");
        Assert.Equal(NodeType.FunctionMember, activate.Properties.Type);
        Assert.Null(activate.Properties.IsPrivate);

        // extension.ts starts the language server, which logs
        Assert.Contains(items.Links(), l =>
            l.Source.StartsWith("Dependinator*VsCodeExt.src.extension*ts.")
            && l.Target == "Dependinator*VsCodeExt.src.languageServer*ts.startLanguageServer"
        );
        Assert.True(
            items.HasLink(
                "Dependinator*VsCodeExt.src.languageServer*ts.startLanguageServer",
                "Dependinator*VsCodeExt.src.logger*ts.log"
            )
        );
        Assert.True(items.Nodes().Count() > 100, $"Only {items.Nodes().Count()} nodes");
        Assert.True(items.Links().Count() > 150, $"Only {items.Links().Count()} links");
    }
}
