using Dependinator.Core.Rpc;

namespace Dependinator.Core.Claude;

// Asks Claude with the user's own Anthropic API key (Dependinator.Claude). The UI calls it over
// the RPC tunnel: in VS Code the LSP process sends the request, with the key the extension keeps
// in its secret storage; the web host sends it in process, with the key from ANTHROPIC_API_KEY.
// The standalone browser app has no implementation, so the UI offers nothing that needs it there.
[Rpc]
interface IClaudeService
{
    // Claude's answer to the prompt: JSON that matches the schema (structured output). Without a
    // key, the user is asked for one first where the host can (VS Code).
    Task<Result<string>> AskAsync(string prompt, string jsonSchema);
}
