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
