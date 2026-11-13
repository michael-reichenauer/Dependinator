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

    // A root keeps the name it had in the last parse, so the nodes of a source do not change
    // names (and lose their layout) with what happens to the other sources.
    [Fact]
    public void Combine_ShouldKeepTheRootNamesOfTheLastParse_WhenTheSourceBeforeFails()
    {
        const string First = "/repo/a/app/tsconfig.json";
        const string Second = "/repo/services/app/tsconfig.json";
        const string SecondRoot = "app (services\u2215app)";
        ParsedSource[] known = [new(First, "app"), new(Second, SecondRoot)];

        var items = AssertOk(
            WorkspaceItems.Combine(
                Workspace,
                [(First, new Error("It failed.")), (Second, Source("app", Node("app.main*ts")))],
                known
            )
        );

        // "app" is still the failed source's, whose nodes the model keeps
        Assert.Equal([SecondRoot, $"{SecondRoot}.main*ts"], NodeNames(items));
        Assert.Equal([new ParsedSource(Second, SecondRoot)], items.Select(i => i.Source).OfType<ParsedSource>());
    }

    [Fact]
    public void Combine_ShouldKeepARootsGivenName_WhenTheSourceItWasToldApartFromIsLeftOut()
    {
        const string Second = "/repo/services/app/tsconfig.json";
        const string SecondRoot = "app (services\u2215app)";

        var items = AssertOk(
            WorkspaceItems.Combine(
                Workspace,
                [(Second, Source("app", Node("app.main*ts")))],
                [new("/repo/a/app/tsconfig.json", "app"), new(Second, SecondRoot)]
            )
        );

        Assert.Equal([SecondRoot, $"{SecondRoot}.main*ts"], NodeNames(items));
    }

    // A source added to the model does not take the name of a root that is there already, also
    // when it comes before it in the list.
    [Fact]
    public void Combine_ShouldRenameANewSource_WhenASourceAfterItHasTheRootName()
    {
        const string New = "/repo/app/tsconfig.json";
        const string Old = "/repo/services/app/tsconfig.json";

        var items = AssertOk(
            WorkspaceItems.Combine(
                Workspace,
                [(New, Source("app", Node("app.new*ts"))), (Old, Source("app", Node("app.old*ts")))],
                [new(Old, "app")]
            )
        );

        Assert.Equal(["app (app)", "app (app).new*ts", "app", "app.old*ts"], NodeNames(items));
    }

    // A saved name is the root's own only when it is its name now, or that name with what was
    // added to tell it apart; a root whose name changed is named anew.
    [Theory]
    [InlineData("web", "shop", "shop")]
    [InlineData("app (legacy)", "app", "app")]
    [InlineData("app (web)", "app", "app (web)")]
    [InlineData("app (web 2)", "app", "app (web 2)")]
    [InlineData("app (webs)", "app", "app")]
    public void Combine_ShouldNameARootAnew_WhenItsOwnNameChanged(string knownName, string name, string expected)
    {
        const string Path = "/repo/web/package.json";

        var items = AssertOk(WorkspaceItems.Combine(Workspace, [(Path, Source(name))], [new(Path, knownName)]));

        Assert.Equal([expected], NodeNames(items));
    }

    // The name a root had is free again once its source has another name.
    [Fact]
    public void Combine_ShouldGiveARootTheNameThatAnotherSourceGaveUp()
    {
        const string Renamed = "/repo/p/package.json";
        const string New = "/repo/q/package.json";

        var items = AssertOk(
            WorkspaceItems.Combine(Workspace, [(Renamed, Source("shop")), (New, Source("web"))], [new(Renamed, "web")])
        );

        Assert.Equal(["shop", "web"], NodeNames(items));
    }

    // A source that is left out of the model for now still has its root name: the one parsed
    // instead does not take it, so both can be in the model again with the names they had.
    [Fact]
    public void Combine_ShouldHoldTheRootName_OfASourceThatIsLeftOut()
    {
        const string Dev = "/repo/dev/main.bicep";
        const string Prod = "/repo/prod/main.bicep";

        var items = AssertOk(
            WorkspaceItems.Combine(Workspace, [(Prod, Source("main*bicep"))], [new(Dev, "main*bicep")])
        );

        Assert.Equal(["main*bicep (prod)"], NodeNames(items));
    }

    // Two sources can have had one root name, each parsed while the other was not found (on
    // another branch). The one in the model keeps it, not the one that is left out.
    [Fact]
    public void Combine_ShouldLetTheSourceInTheModelKeepARootName_ThatALeftOutSourceHadToo()
    {
        const string LeftOut = "/repo/a/package.json";
        const string Included = "/repo/b/package.json";

        var items = AssertOk(
            WorkspaceItems.Combine(
                Workspace,
                [(Included, Source("app", Node("app.main*js")))],
                [new(LeftOut, "app"), new(Included, "app")]
            )
        );

        Assert.Equal(["app", "app.main*js"], NodeNames(items));
    }

    // Within one source a node can be given more than once (a project built for two frameworks),
    // each item adding to it as in the source's own model.
    [Fact]
    public void Combine_ShouldKeepEveryItemOfANode_WithinOneSource()
    {
        var items = AssertOk(
            WorkspaceItems.Combine(
                Workspace,
                [("/repo/App.sln", Source("App*sln", Node("Core.Type"), Node("Core.Type"), Node("Core.Other")))]
            )
        );

        Assert.Equal(["App*sln", "Core.Type", "Core.Type", "Core.Other"], NodeNames(items));
    }

    // Two projects of one name in two solutions are not the project the solutions share: their
    // nodes cannot be told apart by name, which the user is told.
    [Fact]
    public void Combine_ShouldReportAProjectNamedLikeAProjectOfAnotherSource()
    {
        var items = AssertOk(
            WorkspaceItems.Combine(
                Workspace,
                [
                    ("/repo/a/App.sln", Source("App*sln", Project("Core", "/repo/a/Core/Core.csproj"))),
                    (
                        "/repo/b/App.sln",
                        Source(
                            "App*sln",
                            Project("Core", "/repo/b/Core/Core.csproj"),
                            Project("Core", "/repo/b/Core/Core.csproj"),
                            Project("Shared", "/repo/Shared/Shared.csproj")
                        )
                    ),
                    ("/repo/c/Other.sln", Source("Other*sln", Project("Shared", "/repo/Shared/Shared.csproj"))),
                ]
            )
        );

        Assert.Equal(["App*sln", "Core", "App*sln (b)", "Shared", "Other*sln"], NodeNames(items));
        var problem = Assert.Single(items.Select(i => i.Problem).OfType<ParseProblem>());
        Assert.Equal(
            "App.sln has a project named like a project of another source. "
                + "The two are shown as one: leave one of the sources out of the model.",
            problem.Message
        );
        Assert.Equal("Core", problem.NodeName);
    }

    static Item Project(string name, string file) =>
        new(new Node(name, new() { Type = NodeType.Assembly, FileSpan = new FileSpan(file, 0, 0) }), null);

    static Result<IReadOnlyList<Item>> Source(string rootName, params Item[] items) =>
        (IReadOnlyList<Item>)[new Item(new Node(rootName, new() { Type = NodeType.Solution }), null), .. items];

    static Item Node(string name, string? parent = null) => new(new Node(name, new() { Parent = parent }), null);

    static Item Link(string source, string target) => new(null, new Link(source, target, new()));

    static Item Problem(string message) => new(null, null, Problem: new ParseProblem(message, 3, "Core"));

    static IEnumerable<string> NodeNames(IReadOnlyList<Item> items) =>
        items.Select(i => i.Node).OfType<Node>().Select(node => node.Name);
}
