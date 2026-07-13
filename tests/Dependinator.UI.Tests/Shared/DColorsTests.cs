using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Shared;

public class DColorsTests
{
    [Fact]
    public void CustomNodeColors_ShouldOfferTheSixSharedAccentColors()
    {
        // The container palette offers the same accents as the icon tints, so the two color
        // pickers stay one mental model.
        Assert.Equal(
            ColorUtil.AccentColors.Select(accent => accent.Name),
            DColors.CustomNodeColors.Select(color => color.Name)
        );
    }

    [Fact]
    public void CustomNodeColors_ShouldBeValidDistinctHexPairs()
    {
        Assert.All(DColors.CustomNodeColors, color => Assert.Matches("^#[0-9A-F]{6}$", color.Border));
        Assert.All(DColors.CustomNodeColors, color => Assert.Matches("^#[0-9A-Fa-f]{6}$", color.Background));
        Assert.Equal(
            DColors.CustomNodeColors.Count,
            DColors.CustomNodeColors.Select(color => color.Border).Distinct().Count()
        );
    }

    [Fact]
    public void IsCustomNodeColor_ShouldValidateNames()
    {
        Assert.True(DColors.IsCustomNodeColor("Blue"));
        Assert.True(DColors.IsCustomNodeColor("Rose"));
        Assert.False(DColors.IsCustomNodeColor("Pink"));
        Assert.False(DColors.IsCustomNodeColor(""));
    }

    [Fact]
    public void CustomNodeColorByName_ShouldResolveBorderAndBackground()
    {
        var (border, background) = DColors.CustomNodeColorByName("Teal");

        Assert.StartsWith("#", border);
        Assert.StartsWith("#", background);
        Assert.NotEqual(border, background);
    }
}
