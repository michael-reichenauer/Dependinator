using Dependinator.Core.Claude;
using Dependinator.Core.Rpc;

namespace Dependinator.Core.VsCodeModels;

// Asks a language model that VS Code offers to extensions (its Language Model API, such as GitHub
// Copilot's models), the "Copilot" AI provider. Only the VS Code host has it: the LSP forwards the
// requests to the extension (DependinatorVsCode/src/languageModels.ts), which makes them. The web
// host and the standalone browser app have no implementation, so the UI offers it only in VS Code.
[Rpc]
interface IVsCodeModelService
{
    // The models VS Code offers now; none without Copilot (or another extension that adds models).
    Task<VsCodeModel[]> GetModelsAsync();

    // The model's answer to the prompt: JSON that matches the schema. modelId picks the model
    // (VsCodeModel.Id); without one, or when it is gone, Copilot's first model answers. The first
    // request shows VS Code's consent dialog, so it is made only when the user asked. An error
    // when there is no model, the user declined, or the model did not answer in the format.
    Task<Result<string>> AskAsync(string prompt, string jsonSchema, string? modelId);

    // Like AskAsync, but the model may first look things up with Dependinator's model tools (the
    // MCP tools the host serves, see IModelToolsServer), as often as it needs up to a limit. The
    // LSP runs the tool loop and the tools; the extension asks the model one turn at a time. The
    // answer says how many tool calls it took; its cost is unknown (the user's Copilot plan).
    Task<Result<ToolsAnswer>> AskWithModelToolsAsync(string prompt, string jsonSchema, string? modelId);
}

// A model VS Code offers: Id names it in AskAsync, Name is for the user ("GPT-5 mini"), Vendor the
// extension that adds it ("copilot").
record VsCodeModel(string Id, string Name, string Vendor, string Family, int MaxInputTokens)
{
    // One of GitHub Copilot's own models, which the Copilot provider asks when none is picked.
    public bool IsCopilots => Vendor == "copilot";
}
