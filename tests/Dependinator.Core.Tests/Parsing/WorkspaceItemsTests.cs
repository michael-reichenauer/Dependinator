using Dependinator.Core.Parsing;

namespace Dependinator.Core.Tests.Parsing;

public class WorkspaceItemsTests
{
    const string Workspace = "/repo/.workspace";

    [Fact]
    public void Combine_ShouldKeepTheItemsOfEverySource_AndNameItsRoot()
    {
        var items = AssertOk(
            WorkspaceItems.Combine(
                Workspace,
                [
                    ("/repo/App.sln", Source("App*sln", Node("Core", parent: "App*sln"), Node("Core.Ns.Type"))),
                    ("/repo/web/tsconfig.json", Source("web", Node("web.src.main*ts"), Link("web.src.main*ts", "fs"))),
                ]
            )
        );

        Assert.Equal(["App*sln", "Core", "Core.Ns.Type", "web", "web.src.main*ts"], NodeNames(items));
        Assert.Equal(
            [new ParsedSource("/repo/App.sln", "App*sln"), new ParsedSource("/repo/web/tsconfig.json", "web")],
            items.Select(i => i.Source).OfType<ParsedSource>()
        );
        Assert.Contains(items, i => i.Link is { Source: "web.src.main*ts", Target: "fs" });
        Assert.DoesNotContain(items, i => i.Problem is not null);
    }

    [Fact]
    public void Combine_ShouldReportAFailedSourceAsAProblem_AndKeepTheOthers()
    {
        var items = AssertOk(
            WorkspaceItems.Combine(
                Workspace,
                [("/repo/App.sln", Source("App*sln")), ("/repo/web/tsconfig.json", new Error("Node.js was not found."))]
            )
        );

        Assert.Equal(["App*sln"], NodeNames(items));
        var problem = Assert.Single(items.Select(i => i.Problem).OfType<ParseProblem>());
        Assert.Equal("web could not be parsed. Node.js was not found.", problem.Message);
        Assert.Equal(1, problem.ErrorCount);

        // No parsed-source item for the failed one: that is how the model knows to keep its nodes
        var parsed = Assert.Single(items.Select(i => i.Source).OfType<ParsedSource>());
        Assert.Equal("/repo/App.sln", parsed.Path);
    }

    [Fact]
    public void Combine_ShouldReturnError_WhenEverySourceFailed()
    {
        var e = AssertError(
            WorkspaceItems.Combine(
                Workspace,
                [
                    ("/repo/App.sln", new Error("No C# projects could be loaded.")),
                    ("/repo/web/tsconfig.json", new Error("Node.js was not found.")),
                ]
            )
        );

        Assert.Equal("None of the 2 sources could be parsed. No C# projects could be loaded.", e.Message);
    }

    [Fact]
    public void Combine_ShouldReturnTheError_WhenTheOnlySourceFailed()
    {
        var e = AssertError(
            WorkspaceItems.Combine(Workspace, [("/repo/App.sln", new Error("'App.sln' contains only test projects."))])
        );

        Assert.Equal("'App.sln' contains only test projects.", e.Message);
    }

    [Fact]
    public void Combine_ShouldReturnError_WithoutSources()
    {
        var e = AssertError(WorkspaceItems.Combine(Workspace, []));

        Assert.Equal("No sources are chosen for this workspace model.", e.Message);
    }

    [Fact]
    public void Combine_ShouldPutTheFolderInTheNamesOfASource_WhenItsRootNameIsTaken()
    {
        var items = AssertOk(
            WorkspaceItems.Combine(
                Workspace,
                [
                    ("/repo/infra/main.bicep", Source("main*bicep", Node("main*bicep.web"))),
                    (
                        "/repo/infra/test.env/main.bicep",
                        Source(
                            "main*bicep",
                            Node("main*bicep.web", parent: "main*bicep"),
                            Node("main*bicepX"),
                            Link("main*bicep.web", "main*bicep"),
                            new Item(null, null, new LineDescription("main*bicep.web", "$Externals.vnet", "uses")),
                            new Item(null, null, Problem: new ParseProblem("2 errors", 2, "main*bicep"))
                        )
                    ),
                ]
            )
        );

        const string Root = "main*bicep (infra\u2215test*env)";
        Assert.Equal(["main*bicep", "main*bicep.web", Root, $"{Root}.web", "main*bicepX"], NodeNames(items));
        Assert.Equal(Root, items.First(i => i.Node?.Name == $"{Root}.web").Node!.Properties.Parent);
        Assert.Contains(items, i => i.Link is { } link && link.Source == $"{Root}.web" && link.Target == Root);
        Assert.Contains(items, i => i.LineDescription == new LineDescription($"{Root}.web", "$Externals.vnet", "uses"));
        Assert.Contains(items, i => i.Problem == new ParseProblem("2 errors", 2, Root));
        Assert.Contains(items, i => i.Source == new ParsedSource("/repo/infra/test.env/main.bicep", Root));
    }

    [Fact]
    public void Combine_ShouldRenameASourceWhoseRootIsNamedLikeANodeOfAnother()
    {
        // A project of the solution and a script project folder of the same name
        var items = AssertOk(
            WorkspaceItems.Combine(
                Workspace,
                [
                    ("/repo/App.sln", Source("App*sln", Node("web", parent: "App*sln"), Node("web.Ns.Type"))),
                    ("/repo/src/web/tsconfig.json", Source("web", Node("web.main*ts"))),
                ]
            )
        );

        Assert.Equal(
            ["App*sln", "web", "web.Ns.Type", "web (src\u2215web)", "web (src\u2215web).main*ts"],
            NodeNames(items)
        );
    }

    [Fact]
    public void Combine_ShouldNumberTheRoot_WhenTheFolderNameIsTakenToo()
    {
        var items = AssertOk(
            WorkspaceItems.Combine(
                Workspace,
                [("/repo/tsconfig.json", Source("repo", Node("repo (2)"))), ("/repo/package.json", Source("repo"))]
            )
        );

        Assert.Contains(items, i => i.Source == new ParsedSource("/repo/package.json", "repo (3)"));
    }

    [Fact]
    public void Combine_ShouldShowAProjectOfTwoSolutionsOnce_InTheFirst()
    {
        var items = AssertOk(
            WorkspaceItems.Combine(
                Workspace,
                [
                    ("/repo/All.sln", Source("All*sln", Node("Core", parent: "All*sln"), Problem("3 errors in Core"))),
                    (
                        "/repo/Tools.sln",
                        Source(
                            "Tools*sln",
                            Node("Core", parent: "Tools*sln"),
                            Node("Tool", parent: "Tools*sln"),
                            Link("Tool", "Core"),
                            Problem("3 errors in Core")
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["All*sln", "Core", "Tools*sln", "Tool"], NodeNames(items));
        Assert.Equal("All*sln", items.First(i => i.Node?.Name == "Core").Node!.Properties.Parent);
        Assert.Contains(items, i => i.Link is { Source: "Tool", Target: "Core" });
        Assert.Single(items, i => i.Problem is not null);
    }

    static Result<IReadOnlyList<Item>> Source(string rootName, params Item[] items) =>
        (IReadOnlyList<Item>)[new Item(new Node(rootName, new() { Type = NodeType.Solution }), null), .. items];

    static Item Node(string name, string? parent = null) => new(new Node(name, new() { Parent = parent }), null);

    static Item Link(string source, string target) => new(null, new Link(source, target, new()));

    static Item Problem(string message) => new(null, null, Problem: new ParseProblem(message, 3, "Core"));

    static IEnumerable<string> NodeNames(IReadOnlyList<Item> items) =>
        items.Select(i => i.Node).OfType<Node>().Select(node => node.Name);
}
