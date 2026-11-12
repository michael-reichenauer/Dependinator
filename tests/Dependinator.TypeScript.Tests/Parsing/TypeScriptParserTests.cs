using Dependinator.Core.Parsing;
using Dependinator.TypeScript.Tests.Parsing.Utils;

namespace Dependinator.TypeScript.Tests.Parsing;

// Parses Samples/shop, a small TypeScript project (see TypeScriptFixture), and checks the model
// built from it: node names encode the folders and files ("shop.src.cart*ts.Cart.add"), what a
// file does not export is private, and links follow what the compiler resolves each name to.
[Collection(nameof(TypeScriptCollection))]
public class TypeScriptParserTests(TypeScriptFixture fixture)
{
    const string Cart = "shop.src.cart*ts.Cart";
    const string Model = "shop.src.model*ts";
    const string Main = "shop.src.main*ts";
    const string Pricing = "shop.src.lib.pricing*ts";

    IReadOnlyList<Item> Items => fixture.Shop;

    [Theory]
    [InlineData("/repo/web/tsconfig.json")]
    [InlineData("C:\\repo\\web\\TSCONFIG.json")]
    [InlineData("/repo/web/jsconfig.json")]
    [InlineData("/repo/tool/package.json")]
    public void CanParse_ShouldBeTrue_ForProjectFiles(string path)
    {
        Assert.True(TypeScriptFixture.Parser().CanParse(path));
    }

    [Theory]
    [InlineData("/repo/Dependinator.sln")]
    [InlineData("/repo/web/src/main.ts")]
    [InlineData("/repo/web/tsconfig.build.json")]
    [InlineData("My design")]
    public void CanParse_ShouldBeFalse_ForOtherPaths(string path)
    {
        Assert.False(TypeScriptFixture.Parser().CanParse(path));
    }

    [Fact]
    public async Task ParseAsync_ShouldReturnError_WhenFileIsMissing()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"Missing-{Guid.NewGuid()}", "tsconfig.json");

        var error = AssertError(await TypeScriptFixture.Parser().ParseAsync(missingPath, SolutionParseOptions.Default));

        Assert.Contains("not found", error.Message);
    }

    [Fact]
    public async Task ParseAsync_ShouldReturnTheHelpersError_WhenTheProjectHasNoSource()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dep-ts-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "package.json");
            await File.WriteAllTextAsync(path, "{ \"name\": \"empty\" }");

            var error = AssertError(await TypeScriptFixture.Parser().ParseAsync(path, SolutionParseOptions.Default));

            Assert.Contains("No TypeScript or JavaScript files found", error.Message);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Root_ShouldBeThePackage_WithoutItsScope()
    {
        var root = Items.Node("shop");

        Assert.Equal(NodeType.Solution, root.Properties.Type);
        Assert.StartsWith("A small shop", root.Properties.Description);
    }

    [Fact]
    public void Files_ShouldBeNodes_NamedByTheirFoldersWithTheDotEncoded()
    {
        var file = Items.Node(Pricing);

        Assert.Equal(NodeType.SourceFile, file.Properties.Type);
        Assert.Equal(Path.Combine(Root.ShopFolderPath, "src", "lib", "pricing.ts"), file.Properties.FileSpan!.Path);
        Assert.Equal(0, file.Properties.FileSpan.StartLine);
    }

    [Fact]
    public void Folders_ShouldBeNodes_BetweenTheProjectAndItsFiles()
    {
        Assert.Equal(NodeType.Folder, Items.Node("shop.src").Properties.Type);
        Assert.Equal(NodeType.Folder, Items.Node("shop.src.lib").Properties.Type);
        Assert.Equal(2, Items.Nodes().Count(n => n.Properties.Type == NodeType.Folder));
    }

    [Fact]
    public void Files_ShouldBeDescribedByTheirTopComment()
    {
        Assert.Equal("The shop's entry point: starts the server.", Items.Node(Main).Properties.Description);
        Assert.Equal("The shop's data: what is sold and what it costs.", Items.Node(Model).Properties.Description);
        // A file that starts with its imports and a declaration's own comment has none
        Assert.Null(Items.Node("shop.src.cart*ts").Properties.Description);
    }

    [Fact]
    public void Functions_ShouldBeRows_WithSignatureCommentAndLines()
    {
        var start = Items.Node($"{Main}.start");

        Assert.Equal(NodeType.FunctionMember, start.Properties.Type);
        Assert.Equal("(port?: number): Promise<void>", start.Properties.Signature);
        Assert.Equal("Starts the shop.", start.Properties.Description);
        Assert.Null(start.Properties.IsPrivate);
        // 0-based lines, from the function keyword (after its comment) to the closing brace
        Assert.Equal(15, start.Properties.FileSpan!.StartLine);
        Assert.Equal(22, start.Properties.FileSpan.EndLine);
    }

    [Fact]
    public void Functions_ShouldCoverArrowFunctionsDefaultExportsAndOverloads()
    {
        // export const load = async (id: string) => ...
        var load = Items.Node("shop.src.store*ts.load");
        Assert.Equal(NodeType.FunctionMember, load.Properties.Type);
        Assert.Equal("(id: string): Promise<Item | undefined>", load.Properties.Signature);

        // export default function stop()
        Assert.Equal(NodeType.FunctionMember, Items.Node($"{Main}.stop").Properties.Type);

        // Three declarations of price (two overloads and the implementation) are one node
        Assert.Single(Items.Nodes(), n => n.Name == $"{Pricing}.price");
        Assert.Equal("(item: Item): number", Items.Node($"{Pricing}.price").Properties.Signature);
    }

    [Fact]
    public void WhatAFileDoesNotExport_ShouldBePrivate()
    {
        Assert.True(Items.Node($"{Pricing}.round").Properties.IsPrivate);
        Assert.True(Items.Node($"{Main}.server").Properties.IsPrivate);
        Assert.True(Items.Node("shop.src.store*ts.cache").Properties.IsPrivate);
        Assert.Null(Items.Node($"{Pricing}.price").Properties.IsPrivate);
        Assert.Null(Items.Node("shop.src.store*ts.store").Properties.IsPrivate);
    }

    [Fact]
    public void Variables_ShouldBeRows_WithTheirDeclaredOrInferredType()
    {
        var server = Items.Node($"{Main}.server");
        Assert.Equal(NodeType.VariableMember, server.Properties.Type);
        Assert.Equal(": Server | undefined", server.Properties.Signature);

        // Inferred: the kind of a literal, and a type of the language's own library
        Assert.Equal(": number", Items.Node("shop.src.store*ts.defaultLimit").Properties.Signature);
        Assert.Equal(": Map<string, Item>", Items.Node("shop.src.store*ts.cache").Properties.Signature);
    }

    [Fact]
    public void Types_ShouldBeNodes_OfTheirKind()
    {
        Assert.Equal(NodeType.ClassType, Items.Node(Cart).Properties.Type);
        Assert.Equal("The items a customer is about to buy.", Items.Node(Cart).Properties.Description);
        Assert.Equal(NodeType.InterfaceType, Items.Node($"{Model}.Priced").Properties.Type);
        Assert.Equal(NodeType.EnumType, Items.Node($"{Model}.Currency").Properties.Type);
        Assert.Equal(NodeType.Type, Items.Node($"{Model}.Money").Properties.Type);
    }

    [Fact]
    public void ClassMembers_ShouldBeRows_OfTheirKind()
    {
        var constructor = Items.Node($"{Cart}.constructor");
        Assert.Equal(NodeType.ConstructorMember, constructor.Properties.Type);
        Assert.Equal("(id: string, store: Store)", constructor.Properties.Signature);

        var add = Items.Node($"{Cart}.add");
        Assert.Equal(NodeType.MethodMember, add.Properties.Type);
        Assert.Equal("(id: string, quantity?: number): Promise<void>", add.Properties.Signature);

        // get price(): Money
        var price = Items.Node($"{Cart}.price");
        Assert.Equal(NodeType.PropertyMember, price.Properties.Type);
        Assert.Equal(": Money", price.Properties.Signature);

        // onCleared = (): void => { ... } is a method, written as a property
        Assert.Equal(NodeType.MethodMember, Items.Node($"{Cart}.onCleared").Properties.Type);
        Assert.Equal(NodeType.PropertyMember, Items.Node($"{Cart}.count").Properties.Type);
    }

    [Fact]
    public void ClassMembers_ShouldBePrivate_WhenDeclaredPrivate()
    {
        Assert.True(Items.Node($"{Cart}.total").Properties.IsPrivate);
        Assert.True(Items.Node($"{Cart}.#items").Properties.IsPrivate);
        // constructor(private readonly store: Store): the parameter is a property too
        var store = Items.Node($"{Cart}.store");
        Assert.Equal(NodeType.PropertyMember, store.Properties.Type);
        Assert.True(store.Properties.IsPrivate);
        Assert.Null(Items.Node($"{Cart}.add").Properties.IsPrivate);
    }

    [Fact]
    public void InterfaceEnumAndObjectTypeMembers_ShouldBeRows()
    {
        Assert.Equal(NodeType.PropertyMember, Items.Node($"{Model}.Item.id").Properties.Type);
        Assert.Equal("?: string", Items.Node($"{Model}.Item.name").Properties.Signature);
        Assert.Equal(NodeType.MethodMember, Items.Node($"{Model}.Priced.describe").Properties.Type);
        // onChange: (item: Item) => void is a method, written as a property
        Assert.Equal("(item: Item): void", Items.Node($"{Model}.Item.onChange").Properties.Signature);
        Assert.Equal(NodeType.FieldMember, Items.Node($"{Model}.Currency.Sek").Properties.Type);
        Assert.Equal(": number", Items.Node($"{Model}.Money.amount").Properties.Signature);
    }

    [Fact]
    public void Links_ShouldGoFromTheUsingMember_ToWhatItUses()
    {
        // new Cart(...), cart.add(...), a variable of another file
        Assert.True(Items.HasLink($"{Main}.start", Cart));
        Assert.True(Items.HasLink($"{Main}.start", $"{Cart}.add"));
        Assert.True(Items.HasLink($"{Main}.start", "shop.src.store*ts.store"));
        // A call inside a callback belongs to the method around it
        Assert.True(Items.HasLink($"{Cart}.total", $"{Pricing}.price"));
        // this.store.load(id): the property, and the method of its interface
        Assert.True(Items.HasLink($"{Cart}.add", $"{Cart}.store"));
        Assert.True(Items.HasLink($"{Cart}.add", "shop.src.store*ts.Store.load"));
        // A type in a declaration
        Assert.True(Items.HasLink($"{Cart}.price", $"{Model}.Money"));
        Assert.Equal(NodeType.MethodMember, Items.Link($"{Main}.start", $"{Cart}.add").Properties.TargetType);
    }

    [Fact]
    public void Links_ShouldFollowTheTypesOfTheLanguagesLibrary()
    {
        // price(item) reads item.price.amount, and "item" comes out of Array.reduce's callback:
        // without the library's types it would be untyped and the links missing.
        Assert.True(Items.HasLink($"{Pricing}.price", $"{Model}.Money.amount"));
        Assert.True(Items.HasLink($"{Cart}.price", $"{Model}.Currency.Eur"));
        // Nothing of the language itself is a node or a link target
        Assert.DoesNotContain(Items.Links(), l => l.Target.Contains("Promise") || l.Target.Contains("Array"));
    }

    [Fact]
    public void Links_ShouldMarkInheritance()
    {
        Assert.True(Items.Link(Cart, $"{Model}.Entity").Properties.IsInheritance);
        Assert.True(Items.Link(Cart, $"{Model}.Priced").Properties.IsInheritance);
        Assert.True(Items.Link($"{Model}.Item", $"{Model}.Priced").Properties.IsInheritance);
        Assert.Null(Items.Link($"{Main}.start", Cart).Properties.IsInheritance);
    }

    [Fact]
    public void Links_ShouldNotGoToItselfItsContainerOrItsParts()
    {
        Assert.DoesNotContain(Items.Links(), l => l.Source == l.Target);
        Assert.DoesNotContain(Items.Links(), l => l.Source.StartsWith($"{l.Target}."));
        Assert.DoesNotContain(Items.Links(), l => l.Target.StartsWith($"{l.Source}."));
        // But a member uses its siblings
        Assert.True(Items.HasLink($"{Cart}.price", $"{Cart}.total"));
        Assert.True(Items.HasLink($"{Pricing}.price", $"{Pricing}.round"));
    }

    [Fact]
    public void Imports_ShouldBeNoLinks_OnlyWhatIsUsed()
    {
        // cart.ts imports Currency, Entity, Item, Money and Priced: each link starts where it is used
        Assert.Empty(Items.LinkTargetsOf("shop.src.cart*ts"));
        Assert.DoesNotContain(Items.Links(), l => l.Target == Model);
    }

    [Fact]
    public void Imports_ShouldBeLinks_WhenTheyOnlyRunTheFile()
    {
        // import "./setup"
        Assert.True(Items.HasLink(Main, "shop.src.setup*ts"));
        // Top level code is the file's own
        Assert.True(Items.HasLink("shop.src.setup*ts", "shop.src.lib.config*ts.configure"));
    }

    [Fact]
    public void Links_ShouldGoToTheDeclaration_ThroughReExportsAndPathAliases()
    {
        // setup.ts imports configure from "./lib" (index.ts re-exports it from config.ts)
        Assert.False(Items.HasLink("shop.src.setup*ts", "shop.src.lib.index*ts"));
        // The re-exporting file passes it on
        Assert.True(Items.HasLink("shop.src.lib.index*ts", "shop.src.lib.config*ts.configure"));
        Assert.True(Items.HasLink("shop.src.lib.index*ts", Pricing));
        // cart.ts imports price from "@lib/pricing" (tsconfig.json's paths)
        Assert.True(Items.HasLink($"{Cart}.total", $"{Pricing}.price"));
    }

    [Fact]
    public void DynamicImports_ShouldLinkToTheFile_AndToWhatIsTakenFromIt()
    {
        // const { price } = await import("./lib/pricing")
        Assert.True(Items.HasLink($"{Main}.start", Pricing));
        Assert.True(Items.HasLink($"{Main}.start", $"{Pricing}.price"));
    }

    [Fact]
    public void InstalledPackages_ShouldBeLinkTargets_ByTheirExportedNames()
    {
        // The model puts a top level name it has no node for under its Externals node
        Assert.Equal(
            NodeType.FunctionMember,
            Items.Link($"{Main}.start", "tiny-http.createServer").Properties.TargetType
        );
        Assert.Equal(NodeType.InterfaceType, Items.Link($"{Main}.server", "tiny-http.Server").Properties.TargetType);
        // server.listen(port) is a use of Server, not a node of its own
        Assert.DoesNotContain(Items.Links(), l => l.Target.StartsWith("tiny-http.Server."));
        Assert.DoesNotContain(Items.Nodes(), n => n.Name.StartsWith("tiny-http"));
    }

    [Fact]
    public void NodesOwnModules_ShouldBeUnderNode()
    {
        // readFileSync is declared by "@types/node" in its module "fs"; process is a global of it
        Assert.True(Items.HasLink($"{Main}.start", "node.fs.readFileSync"));
        Assert.True(Items.HasLink("shop.src.setup*ts", "node.process"));
    }

    [Fact]
    public void MissingPackages_ShouldBeALinkAndAProblem()
    {
        // import chalk from "chalk", which is not installed
        Assert.True(Items.HasLink(Main, "chalk"));

        var problem = Assert.Single(Items, i => i.Problem is not null).Problem!;
        Assert.Contains("chalk is not installed", problem.Message);
        Assert.Equal(1, problem.ErrorCount);
        Assert.Equal("shop", problem.NodeName);
    }

    [Fact]
    public void TestFiles_ShouldBeLeftOut_UnlessIncluded()
    {
        Assert.False(Items.HasNode("shop.src.cart*test*ts"));

        Assert.True(fixture.ShopWithTests.HasNode("shop.src.cart*test*ts"));
        Assert.True(fixture.ShopWithTests.HasLink("shop.src.cart*test*ts.cartIsEmpty", Cart));
    }

    [Fact]
    public async Task PackageJson_ShouldParseTheConfigBesideIt()
    {
        // The same project by its package.json: the tsconfig.json beside it is used
        var packagePath = Path.Combine(Root.ShopFolderPath, "package.json");

        var items = AssertOk(await TypeScriptFixture.Parser().ParseAsync(packagePath, SolutionParseOptions.Default));

        Assert.Equal(Items.Nodes().Select(n => n.Name), items.Nodes().Select(n => n.Name));
        Assert.Equal(Items.Links().Count(), items.Links().Count());
    }
}
