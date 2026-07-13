using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Icons;

namespace Dependinator.UI.Tests.Diagrams;

public class IconLibraryTests
{
    // Icon ids referenced by NodeSvg.IconName() via <use href="#id">; all must exist in IconDefs.
    static readonly string[] ReferencedIconIds =
    [
        "Event",
        "Field",
        "Property",
        "Method",
        "Constructor",
        "Solution",
        "Externals",
        "Assembly",
        "Module",
        "Namespace",
        "Type",
        "Interface",
        "Enum",
        "Struct",
        "Record",
    ];

    [Fact]
    public void GetIcon_ShouldReturnSvg_ForEveryNodeType()
    {
        // Every node type resolves to a concrete svg (mapped ones, or the ModuleIcon fallback).
        foreach (var type in Enum.GetValues<NodeType>())
        {
            Assert.Contains("<svg", Icon.GetIcon(type));
        }
    }

    [Fact]
    public void IconDefs_ShouldContainEveryReferencedIconId()
    {
        var defs = Icon.IconDefs;

        foreach (var id in ReferencedIconIds)
        {
            Assert.Contains($"id=\"{id}\"", defs);
        }
    }

    [Fact]
    public void All_ShouldLoadEverySvgAsValidGroupedIcon()
    {
        var icons = IconLibrary.All;

        // The Default group holds the node-type icons; General holds the extra selectable icons.
        Assert.Equal(19, icons.Count(icon => icon.Group == "Default"));
        Assert.Equal(20, icons.Count(icon => icon.Group == "General"));
        Assert.Equal(icons.Count, icons.Count(icon => icon.Group is "Default" or "General"));
        Assert.All(icons, icon => Assert.Contains("<svg", icon.Svg));
    }

    [Fact]
    public void Get_ShouldReturnCachedSvg_OnRepeatedCalls()
    {
        Assert.Same(IconLibrary.Get("Solution"), IconLibrary.Get("Solution"));
    }

    [Fact]
    public void Get_ShouldFallBackToModuleIcon_WhenNameIsUnknown()
    {
        Assert.Equal(IconLibrary.Get("Module"), IconLibrary.Get("DoesNotExist"));
    }

    [Fact]
    public void Contains_ShouldReturnTrue_ForKnownIcon()
    {
        Assert.True(IconLibrary.Contains("Solution"));
    }

    [Fact]
    public void Contains_ShouldReturnFalse_ForUnknownIcon()
    {
        Assert.False(IconLibrary.Contains("DoesNotExist"));
    }

    [Theory]
    [InlineData("Solution", "Solution")]
    [InlineData("key-vault", "key vault")]
    [InlineData("Message_Queue", "Message Queue")]
    [InlineData("api-gateway_v2", "api gateway v2")]
    public void ToDisplayName_ShouldConvertSeparatorsToSpaces(string name, string expected)
    {
        Assert.Equal(expected, IconLibrary.ToDisplayName(name));
    }

    [Fact]
    public void All_ShouldExposeDisplayName_ForEveryIcon()
    {
        // Current icons are single words, so their display name matches the raw name.
        Assert.All(IconLibrary.All, icon => Assert.Equal(icon.Name, icon.DisplayName));
    }

    [Fact]
    public void Get_ShouldReturnColorVariant_WithSuffixedIds()
    {
        var variant = IconLibrary.Get(IconLibrary.VariantName("Type", "Blue"));

        // The variant is a distinct svg whose ids (including gradient ids and their url(#...)
        // references) are suffixed so it can coexist with the base icon in the shared <defs>.
        Assert.Contains("id=\"Type--Blue\"", variant);
        Assert.Contains("id=\"gradType--Blue\"", variant);
        Assert.Contains("url(#gradType--Blue)", variant);
    }

    [Fact]
    public void Contains_ShouldReturnTrue_ForEveryIconColorVariant_OfEveryIcon()
    {
        foreach (var icon in IconLibrary.All)
        foreach (var color in IconLibrary.IconColors)
        {
            Assert.True(IconLibrary.Contains(IconLibrary.VariantName(icon.Name, color.Name)));
        }
    }

    [Fact]
    public void Contains_ShouldReturnFalse_ForUnknownColorVariant()
    {
        Assert.False(IconLibrary.Contains(IconLibrary.VariantName("Type", "Pink")));
    }

    [Fact]
    public void Recolor_ShouldChangeVioletHues_AndKeepWhiteDetails()
    {
        var variant = IconLibrary.Get(IconLibrary.VariantName("Type", "Blue"));

        // The violet base colors are re-hued away...
        Assert.DoesNotContain("#8E6BFF", variant);
        Assert.DoesNotContain("#6A3DEC", variant);
        // ...while the white detail marks are untouched.
        Assert.Contains("#fff", variant);
    }

    [Fact]
    public void Recolor_ShouldProduceDistinctColors_PerVariant()
    {
        var blue = IconLibrary.Get(IconLibrary.VariantName("Type", "Blue"));
        var teal = IconLibrary.Get(IconLibrary.VariantName("Type", "Teal"));

        Assert.NotEqual(
            blue.Replace("--Blue", "--X", StringComparison.Ordinal),
            teal.Replace("--Teal", "--X", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void All_ShouldNotIncludeColorVariants()
    {
        Assert.DoesNotContain(IconLibrary.All, icon => icon.Name.Contains("--"));
    }

    [Fact]
    public void IconColors_ShouldExposeDistinctSwatches_ForAllSixColors()
    {
        Assert.Equal(6, IconLibrary.IconColors.Count);
        Assert.Equal(6, IconLibrary.IconColors.Select(color => color.Swatch).Distinct().Count());
        Assert.All(IconLibrary.IconColors, color => Assert.Matches("^#[0-9A-F]{6}$", color.Swatch));
        Assert.DoesNotContain(IconLibrary.IconColors, color => color.Swatch == IconLibrary.DefaultSwatch);
    }

    [Fact]
    public void GetIconName_ShouldComposeColorVariant_ForNodeWithIconColor()
    {
        var node = CreateNode(NodeType.ClassType);
        node.CustomIconColor = "Blue";

        Assert.Equal("Type--Blue", Icon.GetIconName(node));
    }

    [Fact]
    public void GetIconName_ShouldApplyColor_ToCustomIcon()
    {
        var node = CreateNode(NodeType.ClassType);
        node.CustomIconName = "Database";
        node.CustomIconColor = "Teal";

        Assert.Equal("Database--Teal", Icon.GetIconName(node));
    }

    [Fact]
    public void GetIconName_ShouldFallBackToBaseIcon_ForUnknownColor()
    {
        var node = CreateNode(NodeType.ClassType);
        node.CustomIconColor = "Pink";

        Assert.Equal("Type", Icon.GetIconName(node));
    }

    static Dependinator.UI.Modeling.Models.Node CreateNode(NodeType type)
    {
        var root = new Dependinator.UI.Modeling.Models.Node("", null!) { Type = NodeType.Root };
        return new Dependinator.UI.Modeling.Models.Node("Node", root) { Type = type };
    }

    [Fact]
    public void IconDefs_ShouldContainVariantsOfEveryReferencedIconId()
    {
        var defs = Icon.IconDefs;

        foreach (var id in ReferencedIconIds)
        foreach (var color in IconLibrary.IconColors)
        {
            Assert.Contains($"id=\"{id}--{color.Name}\"", defs);
        }
    }

    [Fact]
    public void IconDefs_ShouldNotContainDuplicateIds()
    {
        // All icon svgs are concatenated into one <defs>; duplicate ids (e.g. a shared "grad"
        // gradient id) would collide and mis-render, so every id must be unique.
        var ids = System
            .Text.RegularExpressions.Regex.Matches(Icon.IconDefs, "id=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
