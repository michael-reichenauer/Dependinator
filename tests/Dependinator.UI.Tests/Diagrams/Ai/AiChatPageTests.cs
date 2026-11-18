using Dependinator.Core.Claude;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Diagrams.Ai;

// The page a copied prompt is pasted into (Settings › AI): claude.ai's new chat unless the user
// set another web address, such as a Claude Code project.
public class AiChatPageTests
{
    const string ProjectUrl = "https://claude.ai/code/project/chan_0123456789abcdef";

    readonly Config stored = new();
    readonly Mock<IConfigService> config = new();

    public AiChatPageTests()
    {
        config.Setup(c => c.GetAsync()).ReturnsAsync(() => stored);
        config.Setup(c => c.SetAsync(It.IsAny<Action<Config>>())).Callback<Action<Config>>(update => update(stored));
    }

    AiService Service() =>
        new(Mock.Of<IClaudeService>(), Mock.Of<IVsCodeModelService>(), config.Object, Mock.Of<IModelMgr>());

    [Fact]
    public async Task GetChatUrlAsync_ShouldBeClaudeAi_WhenNoneIsSet()
    {
        Assert.Equal("https://claude.ai/new", await Service().GetChatUrlAsync());
    }

    [Fact]
    public async Task SetChatUrlAsync_ShouldKeepTheAddress_AndClearItWhenEmpty()
    {
        var service = Service();
        var changes = 0;
        service.ChoiceChanged += () => changes++;

        Assert.True(await service.SetChatUrlAsync($"  {ProjectUrl} "));
        Assert.Equal(ProjectUrl, stored.AiChatUrl);
        Assert.Equal(ProjectUrl, await service.GetChatUrlAsync());

        Assert.True(await service.SetChatUrlAsync(""));
        Assert.Null(stored.AiChatUrl);
        Assert.Equal(AiChatPage.DefaultUrl, await service.GetChatUrlAsync());
        Assert.Equal(2, changes);
    }

    [Theory]
    [InlineData("claude.ai/new")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not a url")]
    public async Task SetChatUrlAsync_ShouldNotSaveWhatIsNoWebAddress(string url)
    {
        stored.AiChatUrl = ProjectUrl;

        Assert.False(await Service().SetChatUrlAsync(url));
        Assert.Equal(ProjectUrl, stored.AiChatUrl);
    }

    [Fact]
    public void UrlOf_ShouldBeClaudeAi_ForAStoredAddressThatIsNoWebPage()
    {
        Assert.Equal(AiChatPage.DefaultUrl, AiChatPage.UrlOf("javascript:alert(1)"));
        Assert.Equal(AiChatPage.DefaultUrl, AiChatPage.UrlOf(null));
    }

    [Theory]
    [InlineData("https://claude.ai/new", "Open claude.ai")]
    [InlineData(ProjectUrl, "Open claude.ai")]
    [InlineData("https://www.example.com/chat", "Open example.com")]
    public void OpenText_ShouldNameTheSite(string url, string text)
    {
        Assert.Equal(text, AiChatPage.OpenText(url));
    }
}
