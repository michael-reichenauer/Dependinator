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

    [Fact]
    public void HasSourceFiles_ShouldNotCountToolSettings()
    {
        Assert.False(
            ScriptProjects.HasSourceFiles("/app", (_, pattern) => pattern == "*.js" ? ["/app/eslint.config.js"] : [])
        );
        Assert.True(
            ScriptProjects.HasSourceFiles("/app", (_, pattern) =>
                pattern == "*.js" ? ["/app/eslint.config.js", "/app/index.js"] : []
            )
        );
        Assert.True(ScriptProjects.HasSourceFiles("/app", (_, pattern) => pattern == "*.ts" ? ["/app/src/a.ts"] : []));
        Assert.False(ScriptProjects.HasSourceFiles("/app", (_, _) => []));
    }
}
