using Dependinator.Core.Parsing;
using Dependinator.Core.Parsing.Sources;
using Moq;

namespace Dependinator.Core.Tests.Parsing;

public class ParserServiceTests
{
    static readonly IReadOnlyList<Item> SomeItems = [new Item(new Node("A", new()), null)];

    [Fact]
    public async Task ParseAsync_ShouldUseFirstParserThatCanParse()
    {
        var first = ParserFor(path => false);
        var second = ParserFor(path => path.EndsWith(".bicep"));
        var third = ParserFor(path => true);
        var service = new ParserService([first.Object, second.Object, third.Object]);

        var items = AssertOk(await service.ParseAsync("/repo/main.bicep", SolutionParseOptions.Default));

        Assert.Same(SomeItems, items);
        first.Verify(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<SolutionParseOptions>()), Times.Never);
        second.Verify(p => p.ParseAsync("/repo/main.bicep", SolutionParseOptions.Default), Times.Once);
        third.Verify(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<SolutionParseOptions>()), Times.Never);
    }

    [Fact]
    public async Task ParseAsync_ShouldReturnError_WhenNoParserCanParse()
    {
        var parser = ParserFor(path => path.EndsWith(".sln"));
        var service = new ParserService([parser.Object]);

        var e = AssertError(await service.ParseAsync("/repo/notes.txt", SolutionParseOptions.Default));

        Assert.Equal("No parser for '/repo/notes.txt'", e.Message);
        parser.Verify(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<SolutionParseOptions>()), Times.Never);
    }

    [Fact]
    public async Task ParseAsync_ShouldPassDefaultOptions_WhenOptionsIsNull()
    {
        var parser = ParserFor(path => true);
        var service = new ParserService([parser.Object]);

        // Options arrive over JSON-RPC, where a remote caller can still send null.
        AssertOk(await service.ParseAsync("/repo/App.sln", null!));

        parser.Verify(p => p.ParseAsync("/repo/App.sln", SolutionParseOptions.Default), Times.Once);
    }

    [Fact]
    public async Task ParseAsync_ShouldParseEverySourceOfAWorkspaceModel_WithItsOwnParser()
    {
        var solutions = ParserFor(path => path.EndsWith(".sln"), path => RootItems("App*sln"));
        var scripts = ParserFor(path => path.EndsWith("tsconfig.json"), path => RootItems(path.Split('/')[^2]));
        var service = new ParserService([solutions.Object, scripts.Object]);
        var options = new SolutionParseOptions
        {
            IncludeTestProjects = true,
            Sources = ["/repo/App.sln", "/repo/web/tsconfig.json", "/repo/api/tsconfig.json"],
        };

        var items = AssertOk(await service.ParseAsync("/repo/.workspace", options));

        Assert.Equal(["App*sln", "web", "api"], items.Select(i => i.Node).OfType<Node>().Select(n => n.Name));
        Assert.Equal(
            ["/repo/App.sln", "/repo/web/tsconfig.json", "/repo/api/tsconfig.json"],
            items.Select(i => i.Source).OfType<ParsedSource>().Select(s => s.Path)
        );
        // Each source is parsed with the model's options, but as a single source
        var sourceOptions = new SolutionParseOptions { IncludeTestProjects = true };
        solutions.Verify(p =>
            p.ParseAsync("/repo/App.sln", It.Is<SolutionParseOptions>(o => IsSame(o, sourceOptions)))
        );
        scripts.Verify(
            p => p.ParseAsync(It.IsAny<string>(), It.Is<SolutionParseOptions>(o => IsSame(o, sourceOptions))),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task ParseAsync_ShouldParseTheSourcesOfOneParserOneByOne()
    {
        // A parser is written for one parse at a time (MSBuild allows one build in a process)
        var running = 0;
        var mostRunning = 0;
        var parser = new Mock<ISourceParser>(MockBehavior.Strict);
        parser.Setup(p => p.CanParse(It.IsAny<string>())).Returns(true);
        parser
            .Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<SolutionParseOptions>()))
            .Returns(
                async (string path, SolutionParseOptions _) =>
                {
                    mostRunning = Math.Max(mostRunning, Interlocked.Increment(ref running));
                    await Task.Delay(20);
                    Interlocked.Decrement(ref running);
                    return RootItems(path);
                }
            );
        var service = new ParserService([parser.Object]);
        var options = new SolutionParseOptions { Sources = ["/repo/A.sln", "/repo/B.sln", "/repo/C.sln"] };

        AssertOk(await service.ParseAsync("/repo/.workspace", options));

        Assert.Equal(1, mostRunning);
    }

    [Fact]
    public async Task ParseAsync_ShouldReportASourceThatFailsOrThrows_AsAProblemOfTheWorkspaceModel()
    {
        var solutions = ParserFor(path => path.EndsWith(".sln"), path => RootItems("App*sln"));
        var bicep = ParserFor(path => path.EndsWith(".bicep"), path => new Error("3 errors"));
        var scripts = ParserFor(
            path => path.EndsWith("tsconfig.json"),
            path => throw new InvalidOperationException("boom")
        );
        var service = new ParserService([solutions.Object, bicep.Object, scripts.Object]);
        var options = new SolutionParseOptions
        {
            Sources = ["/repo/App.sln", "/repo/main.bicep", "/repo/web/tsconfig.json", "/repo/notes.txt"],
        };

        var items = AssertOk(await service.ParseAsync("/repo/.workspace", options));

        Assert.Equal(["App*sln"], items.Select(i => i.Node).OfType<Node>().Select(n => n.Name));
        Assert.Equal(
            [
                "main.bicep could not be parsed. 3 errors",
                "web could not be parsed. Failed to parse 'web'.",
                "notes.txt could not be parsed. No parser for '/repo/notes.txt'",
            ],
            items.Select(i => i.Problem).OfType<ParseProblem>().Select(p => p.Message)
        );
    }

    [Fact]
    public async Task ParseAsync_ShouldNameTheRootsOfAWorkspaceModelAsInItsLastParse()
    {
        var scripts = ParserFor(path => true, path => RootItems("app"));
        var service = new ParserService([scripts.Object]);
        var options = new SolutionParseOptions
        {
            Sources = ["/repo/a/tsconfig.json", "/repo/b/tsconfig.json"],
            KnownSources = [new("/repo/b/tsconfig.json", "app")],
        };

        var items = AssertOk(await service.ParseAsync("/repo/.workspace", options));

        Assert.Equal(
            [new ParsedSource("/repo/a/tsconfig.json", "app (a)"), new ParsedSource("/repo/b/tsconfig.json", "app")],
            items.Select(i => i.Source).OfType<ParsedSource>()
        );
        scripts.Verify(
            p => p.ParseAsync(It.IsAny<string>(), It.Is<SolutionParseOptions>(o => IsSame(o, new()))),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task ParseAsync_ShouldReturnError_ForAWorkspaceModelWithoutSources()
    {
        var service = new ParserService([ParserFor(path => true).Object]);

        var e = AssertError(await service.ParseAsync("/repo/.workspace", SolutionParseOptions.Default));

        Assert.Equal("No sources are chosen for this workspace model.", e.Message);
    }

    static bool IsSame(SolutionParseOptions options, SolutionParseOptions expected) =>
        options.IncludeTestProjects == expected.IncludeTestProjects
        && options.Sources.SequenceEqual(expected.Sources)
        && options.KnownSources.SequenceEqual(expected.KnownSources);

    static IReadOnlyList<Item> RootItems(string rootName) =>
        [new Item(new Node(rootName, new() { Type = NodeType.Solution }), null)];

    static Mock<ISourceParser> ParserFor(Func<string, bool> canParse, Func<string, Result<IReadOnlyList<Item>>> parse)
    {
        var parser = new Mock<ISourceParser>(MockBehavior.Strict);
        parser.Setup(p => p.CanParse(It.IsAny<string>())).Returns(canParse);
        parser
            .Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<SolutionParseOptions>()))
            .Returns((string path, SolutionParseOptions _) => Task.FromResult(parse(path)));
        return parser;
    }

    static Mock<ISourceParser> ParserFor(Func<string, bool> canParse)
    {
        var parser = new Mock<ISourceParser>(MockBehavior.Strict);
        parser.Setup(p => p.CanParse(It.IsAny<string>())).Returns(canParse);
        parser.Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<SolutionParseOptions>())).ReturnsAsync(SomeItems);
        return parser;
    }
}
