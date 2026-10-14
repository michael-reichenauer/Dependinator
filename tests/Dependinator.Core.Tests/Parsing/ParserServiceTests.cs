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

    static Mock<ISourceParser> ParserFor(Func<string, bool> canParse)
    {
        var parser = new Mock<ISourceParser>(MockBehavior.Strict);
        parser.Setup(p => p.CanParse(It.IsAny<string>())).Returns(canParse);
        parser.Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<SolutionParseOptions>())).ReturnsAsync(SomeItems);
        return parser;
    }
}
