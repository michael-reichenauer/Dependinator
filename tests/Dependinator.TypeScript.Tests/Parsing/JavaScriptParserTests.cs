using Dependinator.Core.Parsing;
using Dependinator.TypeScript.Tests.Parsing.Utils;

namespace Dependinator.TypeScript.Tests.Parsing;

// Parses Samples/tool, a plain JavaScript project (a package.json, no config file) of CommonJS
// and ES module files, where an assignment to exports is a declaration and little has a type.
[Collection(nameof(TypeScriptCollection))]
public class JavaScriptParserTests(TypeScriptFixture fixture)
{
    const string Index = "tool.index*js";
    const string Runner = "tool.lib.runner*js";

    IReadOnlyList<Item> Items => fixture.Tool;

    [Fact]
    public void Root_ShouldBeThePackage_DescribedAsJavaScript()
    {
        var root = Items.Node("tool");

        Assert.Equal(NodeType.Solution, root.Properties.Type);
        Assert.Equal("JavaScript project", root.Properties.Description);
    }

    [Fact]
    public void EveryKindOfSourceFile_ShouldBeFound_WithoutAConfigFile()
    {
        Assert.Equal(NodeType.SourceFile, Items.Node(Index).Properties.Type);
        Assert.Equal(NodeType.SourceFile, Items.Node(Runner).Properties.Type);
        Assert.Equal(NodeType.SourceFile, Items.Node("tool.web.app*mjs").Properties.Type);
    }

    [Fact]
    public void Functions_ShouldShowWhatIsKnown_WithoutTypes()
    {
        // No types to show, and no "any" either
        Assert.Equal("(argv)", Items.Node($"{Index}.main").Properties.Signature);
        // What the compiler can infer is shown
        Assert.Equal("(name): string", Items.Node("tool.web.util*mjs.helper").Properties.Signature);
    }

    [Fact]
    public void CommonJsExports_ShouldBeDeclarations()
    {
        // exports.run = function (text, runner) { ... }
        var run = Items.Node($"{Runner}.run");
        Assert.Equal(NodeType.FunctionMember, run.Properties.Type);
        Assert.Equal("(text, runner)", run.Properties.Signature);
        // module.exports.limit = 10
        Assert.Equal(NodeType.VariableMember, Items.Node($"{Runner}.limit").Properties.Type);
        // module.exports.Runner = Runner names the class, and declares nothing new
        Assert.Single(Items.Nodes(), n => n.Name == $"{Runner}.Runner");
        Assert.Equal(NodeType.ClassType, Items.Node($"{Runner}.Runner").Properties.Type);
    }

    [Fact]
    public void WhatIsNotExported_ShouldBePrivate_InCommonJsToo()
    {
        Assert.True(Items.Node($"{Runner}.format").Properties.IsPrivate);
        Assert.True(Items.Node($"{Index}.unused").Properties.IsPrivate);
        // module.exports = { main }, module.exports.Runner = Runner
        Assert.Null(Items.Node($"{Index}.main").Properties.IsPrivate);
        Assert.Null(Items.Node($"{Runner}.Runner").Properties.IsPrivate);
    }

    [Fact]
    public void Require_ShouldBeAnImport_NotAVariable()
    {
        // const fs = require("fs"); const { run, Runner } = require("./lib/runner");
        Assert.False(Items.HasNode($"{Index}.fs"));
        Assert.False(Items.HasNode($"{Index}.run"));
        Assert.True(Items.HasLink($"{Index}.main", $"{Runner}.run"));
        Assert.True(Items.HasLink($"{Index}.main", $"{Runner}.Runner"));
        // Node's own module, without Node's types installed
        Assert.True(Items.HasLink(Index, "node.fs"));
        Assert.DoesNotContain(Items, i => i.Problem is not null);
    }

    [Fact]
    public void Links_ShouldFollowEsModuleImports()
    {
        Assert.True(Items.HasLink("tool.web.app*mjs.render", "tool.web.util*mjs.helper"));
        Assert.True(Items.HasLink($"{Runner}.Runner.start", $"{Runner}.format"));
    }
}
