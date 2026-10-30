namespace Dependinator.UI.Diagrams.Ai;

// What a place that asks an AI needs from it: one answer to a prompt, lookups in the open model
// with Dependinator's tools while it answers (the Ask panel), or changes to the code.
[Flags]
public enum AiCapability
{
    None = 0,
    Answer = 1,
    ModelTools = 2,
    EditCode = 4,
}

// An AI the app can ask, named by its AiSource: how the provider menus and Settings › AI show it
// (icon, a line on what it uses and costs) and what it can do. Another provider (Copilot, Codex,
// Gemini) is another AiSource value and another entry in All, asked through IAiService.
public sealed record AiProvider(AiSource Source, string Id, string Icon, string Detail, AiCapability Capabilities)
{
    public static readonly AiProvider Claude = new(
        AiSource.Claude,
        "claude",
        MudBlazor.Icons.Material.Outlined.AutoAwesome,
        "Your Anthropic API key, a few cents per answer",
        AiCapability.Answer | AiCapability.ModelTools
    );

    public static readonly AiProvider ClaudeCode = new(
        AiSource.ClaudeCode,
        "code",
        MudBlazor.Icons.Material.Outlined.Terminal,
        "Claude Code on this computer, your Claude login (experimental)",
        AiCapability.Answer | AiCapability.ModelTools | AiCapability.EditCode
    );

    public static readonly AiProvider Chat = new(
        AiSource.Chat,
        "chat",
        MudBlazor.Icons.Material.Outlined.Forum,
        "Copy a prompt into claude.ai or any AI chat, then paste its answer back. Free",
        AiCapability.Answer
    );

    // In the order the menus list them.
    public static readonly IReadOnlyList<AiProvider> All = [Claude, ClaudeCode, Chat];

    public static AiProvider Of(AiSource source) => All.First(p => p.Source == source);

    public string Name => Source.Name();

    public bool Can(AiCapability need) => (Capabilities & need) == need;

    // The main button's text: "Ask Claude Code", or "Ask Claude Code again" once it answered.
    public string AskText(bool again) =>
        Source is AiSource.Chat ? "Use your AI chat"
        : again ? $"Ask {Name} again"
        : $"Ask {Name}";
}

// The providers the user can ask now (Claude Code only where it is turned on, only the AI chat in
// the standalone browser app) and the one they made the default, if any (Settings › AI).
public sealed record AiChoice(IReadOnlyList<AiProvider> Providers, AiSource? Default)
{
    public static readonly AiChoice ChatOnly = new([AiProvider.Chat], null);

    public bool IsDefault(AiProvider provider) => provider.Source == Default;

    // The default, where it can do what is needed, or else the first provider that can.
    public AiProvider? DefaultFor(AiCapability need) =>
        Providers.FirstOrDefault(p => p.Source == Default && p.Can(need)) ?? Providers.FirstOrDefault(p => p.Can(need));

    // Only the providers a place can use (one without a prompt to copy offers no AI chat).
    public AiChoice Where(Func<AiProvider, bool> predicate)
    {
        List<AiProvider> providers = [.. Providers.Where(predicate)];
        return new(providers, providers.Any(p => p.Source == Default) ? Default : null);
    }
}
