using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Icons;

namespace Dependinator.UI.Tests.Diagrams;

public class IconLibraryTests
{
    // Icon ids referenced by NodeSvg.IconName() via <use href="#id">; all must exist in IconDefs.
    static readonly string[] ReferencedIconIds =
    [
        "EventIcon",
        "FieldIcon",
        "PropertyIcon",
        "MethodIcon",
        "ConstructorIcon",
        "SolutionIcon",
        "ExternalsIcon",
        "ModuleIcon",
        "FilesIcon",
        "TypeIcon",
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
    public void All_ShouldLoadEverySvgInTheDefaultGroup()
    {
        var icons = IconLibrary.All;

        Assert.Equal(13, icons.Count);
        Assert.All(icons, icon => Assert.Equal("Default", icon.Group));
        Assert.All(icons, icon => Assert.Contains("<svg", icon.Svg));
    }

    [Fact]
    public void Get_ShouldReturnCachedSvg_OnRepeatedCalls()
    {
        Assert.Same(IconLibrary.Get("SolutionIcon"), IconLibrary.Get("SolutionIcon"));
    }

    [Fact]
    public void Get_ShouldFallBackToModuleIcon_WhenNameIsUnknown()
    {
        Assert.Equal(IconLibrary.Get("ModuleIcon"), IconLibrary.Get("DoesNotExist"));
    }
}
