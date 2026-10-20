using Dependinator.Core.Rpc;

namespace Dependinator.Core.Claude;

// Asks Claude with the user's own Anthropic API key (Dependinator.Claude). The UI calls it over
// the RPC tunnel: in VS Code the LSP process sends the request, with the key the extension keeps
// in its secret storage; the web host sends it in process, with the key from ANTHROPIC_API_KEY.
// The standalone browser app has no implementation, so the UI offers nothing that needs it there.
// The UI checks the key first, since in VS Code the key is entered outside the UI (an input box
// at the top of the window) and the user must be told to look there.
[Rpc]
interface IClaudeService
{
    Task<ClaudeApiKeyState> GetApiKeyStateAsync();

    // Asks the user for a key where the host can (VS Code: an input box); an error saying how to
    // set one where it cannot, or when the user enters none.
    Task<Result> AskUserForApiKeyAsync();

    // Claude's answer to the prompt: JSON that matches the schema (structured output). An error
    // without a key, or when Claude does not accept it (the state is then Rejected).
    Task<Result<string>> AskAsync(string prompt, string jsonSchema);
}

enum ClaudeApiKeyState
{
    Missing,
    Set,

    // Claude did not accept the key on the last request; a new one is needed.
    Rejected,
}
