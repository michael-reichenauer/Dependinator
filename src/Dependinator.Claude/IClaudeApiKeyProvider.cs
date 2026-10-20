namespace Dependinator.Claude;

// Where a host keeps the user's Anthropic API key (from console.anthropic.com, billed per use).
public interface IClaudeApiKeyProvider
{
    // How the user sets or replaces the key in this host, for messages.
    string SetKeyHint { get; }

    // The key, or null when none is set.
    Task<string?> GetApiKeyAsync();

    // Asks the user for a key where the host can (VS Code: an input box); an error otherwise, or
    // when the user gives none.
    Task<Result<string>> AskUserForApiKeyAsync();
}
