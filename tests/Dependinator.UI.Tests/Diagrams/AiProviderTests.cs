using Dependinator.UI.Diagrams.Ai;

namespace Dependinator.UI.Tests.Diagrams;

// The providers a place can ask, and which one its main button asks (AiChoice).
public class AiProviderTests
{
    [Fact]
    public void DefaultFor_ShouldBeTheDefault_WhenItCanDoWhatIsNeeded()
    {
        var choice = new AiChoice(AiProvider.All, AiSource.ClaudeCode);

        Assert.Equal(AiProvider.ClaudeCode, choice.DefaultFor(AiCapability.Answer));
        Assert.Equal(AiProvider.ClaudeCode, choice.DefaultFor(AiCapability.EditCode));
    }

    [Fact]
    public void DefaultFor_ShouldBeTheFirstThatCan_WhenTheDefaultCannot()
    {
        // The AI chat cannot look things up in the model, which the Ask panel needs.
        var choice = new AiChoice(AiProvider.All, AiSource.Chat);

        Assert.Equal(AiProvider.Chat, choice.DefaultFor(AiCapability.Answer));
        Assert.Equal(AiProvider.Claude, choice.DefaultFor(AiCapability.ModelTools));
        Assert.Null(AiChoice.ChatOnly.DefaultFor(AiCapability.ModelTools));
    }

    [Fact]
    public void DefaultFor_ShouldBeTheFirstProvider_WhenThereIsNoDefault()
    {
        var choice = new AiChoice([AiProvider.Claude, AiProvider.Chat], null);

        Assert.Equal(AiProvider.Claude, choice.DefaultFor(AiCapability.Answer));
    }

    [Fact]
    public void Where_ShouldDropTheDefault_WhenItIsLeftOut()
    {
        var choice = new AiChoice(AiProvider.All, AiSource.Chat);

        var withoutChat = choice.Where(p => p.Source is not AiSource.Chat);
        var withChat = choice.Where(p => p.Source is not AiSource.ClaudeCode);

        Assert.Equal([AiProvider.Claude, AiProvider.ClaudeCode], withoutChat.Providers);
        Assert.Null(withoutChat.Default);
        Assert.Equal(AiSource.Chat, withChat.Default);
    }

    [Fact]
    public void AskText_ShouldNameTheProvider_AndSayAgainOnceItAnswered()
    {
        Assert.Equal("Ask Claude Code", AiProvider.ClaudeCode.AskText(again: false));
        Assert.Equal("Ask Claude again", AiProvider.Claude.AskText(again: true));
        Assert.Equal("Use your AI chat", AiProvider.Chat.AskText(again: true));
    }

    [Fact]
    public void All_ShouldGiveEachProviderItsOwnId()
    {
        Assert.Equal(AiProvider.All.Count, AiProvider.All.Select(p => p.Id).Distinct().Count());
        Assert.Equal(Enum.GetValues<AiSource>().Length, AiProvider.All.Count);
    }
}
