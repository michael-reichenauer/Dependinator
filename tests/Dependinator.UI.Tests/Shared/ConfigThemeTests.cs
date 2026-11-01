using System.Text.Json;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Shared;

// Light is the default theme; a Light or Dark choice stored before then is kept, while the old
// default System (stored in every config) counts as unset.
public class ConfigThemeTests
{
    static readonly JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void CurrentTheme_ShouldBeLight_ForANewConfig()
    {
        Assert.Equal(AppTheme.Light, new Config().CurrentTheme);
    }

    [Theory]
    [InlineData("{\"Theme\":0}", 1)]
    [InlineData("{\"Theme\":1}", 1)]
    [InlineData("{\"Theme\":2}", 2)]
    [InlineData("{}", 1)]
    public void CurrentTheme_ShouldMigrateTheOldSetting(string json, int expected)
    {
        Assert.Equal((AppTheme)expected, JsonSerializer.Deserialize<Config>(json, options)!.CurrentTheme);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CurrentTheme_ShouldBeTheSelectedTheme_AfterARoundTrip(int selectedValue)
    {
        var selected = (AppTheme)selectedValue;
        var stored = JsonSerializer.Serialize(new Config { Theme = AppTheme.Dark, SelectedTheme = selected });

        Assert.Equal(selected, JsonSerializer.Deserialize<Config>(stored, options)!.CurrentTheme);
    }
}
