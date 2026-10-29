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

    // Like AskAsync, on a faster and cheaper model, for a plain question (explaining a node, say)
    // rather than a verdict to weigh.
    Task<Result<string>> AskQuicklyAsync(string prompt, string jsonSchema);

    // Like AskQuicklyAsync, but Claude may first look things up with Dependinator's model tools
    // (the MCP tools the host serves, see IModelToolsServer), as many times as it needs up to a
    // limit, before it answers. The answer says how many tool calls it took and what it cost.
    Task<Result<ToolsAnswer>> AskWithModelToolsAsync(string prompt, string jsonSchema);

    // Like AskAsync, through the experimental local route: `claude -p` (Claude Code) on this
    // computer, with the user's own Claude login instead of an API key. The UI offers it only when
    // the user turned it on in Settings. Claude Code may read and search the files in codeFolder
    // (the model's folder), but not change them or run commands; without a folder it has no tools.
    // With withModelTools it may also ask the open diagram's model through Dependinator's MCP
    // tools, where the host serves them (IModelToolsServer). An error when the command is missing
    // or not signed in.
    Task<Result<string>> AskClaudeCodeAsync(string prompt, string jsonSchema, string? codeFolder, bool withModelTools);

    // Lets Claude Code change the code in codeFolder as the prompt asks: it may also edit and
    // create files there, still without running commands (so it cannot build or run tests). The
    // answer is its report, JSON that matches the schema. Can take minutes; an error when stopped.
    Task<Result<string>> FixWithClaudeCodeAsync(string prompt, string jsonSchema, string codeFolder);

    // Stops a running FixWithClaudeCodeAsync, which then returns an error; false when none runs.
    Task<bool> StopClaudeCodeFixAsync();

    // The number of files with uncommitted changes in codeFolder's git repository, to warn before
    // a fix; an error when the folder is not in a git repository (or git is missing).
    Task<Result<int>> CountUncommittedChangesAsync(string codeFolder);
}

// Claude's answer after it used the model tools: JSON that matches the schema, the number of tool
// calls it made, and the requests' cost in US dollars at the API's list prices (null when unknown:
// another model answered a turn).
record ToolsAnswer(string Json, int ToolCalls, decimal? CostUsd);

enum ClaudeApiKeyState
{
    Missing,
    Set,

    // Claude did not accept the key on the last request; a new one is needed.
    Rejected,
}
