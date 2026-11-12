using Dependinator.Core.Shared;

namespace Dependinator.Core.Tests.Shared;

public class ScriptProjectsTests
{
    static readonly string Root = Path.Combine(Path.GetTempPath(), "dep-script-projects");

    static string At(params string[] parts) => Path.Combine([Root, .. parts]);

    static IReadOnlyList<string> Find(
        IReadOnlyList<string> configs,
        IReadOnlyList<string> packages,
        Dictionary<string, string>? texts = null,
        Func<string, bool>? hasSourceFiles = null
    ) =>
        ScriptProjects.Find(
            configs,
            packages,
            path => texts?.GetValueOrDefault(path) ?? "{}",
            hasSourceFiles ?? (_ => true)
        );

    [Fact]
    public void Find_ShouldListEveryConfig_AndNoPackageBesideOne()
    {
        var web = At("web", "tsconfig.json");
        var scripts = At("scripts", "jsconfig.json");

        var projects = Find([web, scripts], [At("web", "package.json")]);

        Assert.Equal([web, scripts], projects);
    }

    [Fact]
    public void Find_ShouldLeaveOutConfigs_ThatAnotherConfigReferences()
    {
        var solution = At("tsconfig.json");
        var core = At("packages", "core", "tsconfig.json");
        var app = At("packages", "app", "tsconfig.app.json");
        var tests = At("tests", "tsconfig.json");
        var texts = new Dictionary<string, string>
        {
            [solution] = """
                {
                    // A solution style config
                    "files": [],
                    "references": [{ "path": "./packages/core" }, { "path": "./packages/app/tsconfig.app.json" }]
                }
                """,
        };

        var projects = Find([solution, core, tests], [], texts);

        Assert.Equal([solution, tests], projects);
        Assert.DoesNotContain(app, projects);
    }

    [Fact]
    public void Find_ShouldListAPackage_OfAPlainJavaScriptProject()
    {
        var tool = At("tool", "package.json");

        Assert.Equal([tool], Find([], [tool]));
    }

    [Fact]
    public void Find_ShouldLeaveOutAPackage_WithoutSourceFiles()
    {
        var tool = At("tool", "package.json");

        Assert.Empty(Find([], [tool], hasSourceFiles: _ => false));
    }

    [Fact]
    public void Find_ShouldLeaveOutAPackage_AboveOrBelowAConfig()
    {
        // The repository's tooling package.json above the projects, and a package inside one
        var web = At("web", "tsconfig.json");

        var projects = Find([web], [At("package.json"), At("web", "widgets", "package.json")]);

        Assert.Equal([web], projects);
    }

    [Fact]
    public void Find_ShouldKeepAPackage_BesideAnotherProjectsFolder()
    {
        // "web2" is not inside "web"
        var web = At("web", "tsconfig.json");
        var web2 = At("web2", "package.json");

        Assert.Equal([web, web2], Find([web], [web2]));
    }

    [Theory]
    [InlineData("index.js")]
    [InlineData("src/a.ts")]
    [InlineData("src/view.tsx")]
    [InlineData("lib/module.mts")]
    [InlineData("lib/common.cts")]
    [InlineData("web/app.mjs")]
    public void HasSourceFiles_ShouldCountWhatTheParserParses(string file)
    {
        Assert.True(HasSourceFiles("eslint.config.js", "README.md", file));
    }

    [Theory]
    [InlineData("eslint.config.js")] // A tool's settings
    [InlineData("types/global.d.ts")] // Declarations only
    [InlineData("types/global.d.mts")]
    [InlineData("dist/index.js")] // What is built
    [InlineData("web/build/app.js")]
    [InlineData("out/main.js")]
    [InlineData("coverage/lcov-report/sorter.js")]
    [InlineData("styles/site.css")]
    public void HasSourceFiles_ShouldNotCountWhatTheParserLeavesOut(string file)
    {
        Assert.False(HasSourceFiles("README.md", file));
    }

    [Fact]
    public void HasSourceFiles_ShouldCountATestFile()
    {
        // The parser leaves test files out, and offers to include them when there is nothing else
        Assert.True(HasSourceFiles("e2e/login.spec.ts"));
    }

    [Fact]
    public void HasSourceFiles_ShouldWalkTheFolderOnce_AndStopAtTheFirstSourceFile()
    {
        var walks = 0;
        var read = 0;
        IEnumerable<string> Files()
        {
            walks++;
            foreach (var file in new[] { "a.md", "index.js", "b.js", "c.js" })
            {
                read++;
                yield return Path.Combine(Root, "app", file);
            }
        }

        Assert.True(ScriptProjects.HasSourceFiles(Path.Combine(Root, "app"), (_, _) => Files()));
        Assert.Equal(1, walks);
        Assert.Equal(2, read);
    }

    // The files as a walk gives them: without those in the folders it is told to skip
    static bool HasSourceFiles(params string[] files)
    {
        var folder = Path.Combine(Root, "app");
        return ScriptProjects.HasSourceFiles(folder, (_, isSkippedFolder) =>
            files
                .Where(file => !file.Split('/').SkipLast(1).Any(isSkippedFolder))
                .Select(file => Path.Combine([folder, .. file.Split('/')]))
        );
    }
}
