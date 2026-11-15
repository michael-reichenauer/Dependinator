using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Tests.Models;

// The choice of a workspace model's sources as it is saved with the model.
public class SourceChoicesTests
{
    const string Folder = "/repo";
    const string App = "/repo/a/package.json";
    const string Web = "/repo/web/package.json";

    [Fact]
    public void Of_ShouldJoinTheFoundSourcesWithTheSavedChoice()
    {
        WorkspaceSource[] saved = [new("A/package.json", false, "app"), new("gone/package.json", true)];

        var choices = SourceChoices.Of(Folder, [App, Web, "/repo/tests/x/package.json"], saved);

        Assert.Equal(
            [
                new SourceChoice(App, "a/package.json", false),
                new SourceChoice(Web, "web/package.json", true, IsNew: true),
                new SourceChoice("/repo/tests/x/package.json", "tests/x/package.json", false, IsNew: true),
            ],
            choices
        );
    }

    [Fact]
    public void ToSaved_ShouldKeepTheRootNames_AndTheSourcesThatAreNotFound()
    {
        WorkspaceSource[] saved = [new("a/package.json", true, "app"), new("gone/package.json", false, "gone")];
        SourceChoice[] choices = [new(App, "a/package.json", false), new(Web, "web/package.json", true, IsNew: true)];

        var result = SourceChoices.ToSaved(choices, saved, new Dictionary<string, string> { [Web] = "web" });

        Assert.Equal(
            [
                new WorkspaceSource("a/package.json", false, "app"),
                new WorkspaceSource("web/package.json", true, "web"),
                new WorkspaceSource("gone/package.json", false, "gone"),
            ],
            result
        );
    }

    // Two sources can have had one root name, each parsed while the other was not found. The
    // name is the parsed one's now: the other must not claim it back when it is found again.
    [Fact]
    public void ToSaved_ShouldForgetTheRootNameOfASourceNotParsed_ThatAParsedSourceHasNow()
    {
        WorkspaceSource[] saved = [new("gone/package.json", true, "app")];
        SourceChoice[] choices = [new(App, "a/package.json", true, IsNew: true)];

        var result = SourceChoices.ToSaved(choices, saved, new Dictionary<string, string> { [App] = "app" });

        Assert.Equal(
            [new WorkspaceSource("a/package.json", true, "app"), new WorkspaceSource("gone/package.json", true)],
            result
        );
    }
}
