using Dependinator.Claude;
using Dependinator.Core.Claude;
using Dependinator.Core.ModelQueries;
using Dependinator.Core.Utils;
using Dependinator.Core.Utils.Logging;
using Dependinator.Core.VsCodeModels;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Dependinator.Lsp.VsCodeModels;

// Requests to the extension, which asks VS Code's Language Model API
// (DependinatorVsCode/src/languageModels.ts): the models it offers, and an answer from one.
public record VsCodeModelsParams
{
    public static readonly string Method = "dependinator/lm/models";
}

public record VsCodeModelsResult(VsCodeModelInfo[]? Models);

public record VsCodeModelInfo(string Id, string Name, string Vendor, string Family, int MaxInputTokens);

public record VsCodeAskParams(string Prompt, string JsonSchema, string? ModelId)
{
    public static readonly string Method = "dependinator/lm/ask";
}

public record VsCodeAskResult(string? Json, string? Error);

// The UI's IVsCodeModelService in VS Code: each call is a request to the extension.
class LspVsCodeModelService(ILanguageServerFacade server, IModelToolsServer modelToolsServer) : IVsCodeModelService
{
    static readonly TimeSpan modelsTimeout = TimeSpan.FromSeconds(10);

    // The extension gives up after 5 minutes; VS Code's consent dialog may wait on the user first.
    static readonly TimeSpan askTimeout = TimeSpan.FromMinutes(10);

    public async Task<VsCodeModel[]> GetModelsAsync()
    {
        try
        {
            using CancellationTokenSource cts = new(modelsTimeout);
            var result = await server
                .SendRequest(VsCodeModelsParams.Method, new VsCodeModelsParams())
                .Returning<VsCodeModelsResult>(cts.Token);
            return
            [
                .. (result.Models ?? []).Select(m => new VsCodeModel(
                    m.Id,
                    m.Name,
                    m.Vendor,
                    m.Family,
                    m.MaxInputTokens
                )),
            ];
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Warn($"Could not list VS Code's language models: {e.Message}");
            return [];
        }
    }

    public async Task<Result<string>> AskAsync(string prompt, string jsonSchema, string? modelId)
    {
        try
        {
            using CancellationTokenSource cts = new(askTimeout);
            var result = await server
                .SendRequest(VsCodeAskParams.Method, new VsCodeAskParams(prompt, jsonSchema, modelId))
                .Returning<VsCodeAskResult>(cts.Token);
            return result.Json is { Length: > 0 } json
                ? json
                : new Error(result.Error ?? "VS Code's language model gave no answer.");
        }
        catch (OperationCanceledException)
        {
            return new Error("VS Code's language model did not answer in time.");
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            return new Error($"Could not ask VS Code's language model: {e.Message}", e);
        }
    }

    public async Task<Result<ToolsAnswer>> AskWithModelToolsAsync(string prompt, string jsonSchema, string? modelId)
    {
        if (modelToolsServer.Address is not { } address)
            return new Error("The model tools are not ready yet. Please try again in a moment.");
        var connected = await (ConnectTools ?? (() => McpAgentTools.ConnectAsync(address)))();
        if (connected is not IAgentTools tools)
            return connected.Error;
        await using (tools)
        {
            try
            {
                return await VsCodeToolsAgent.AskAsync(AskTurnAsync, modelId, prompt, jsonSchema, tools);
            }
            catch (OperationCanceledException)
            {
                return new Error("VS Code's language model did not answer in time.");
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                return new Error($"Could not ask VS Code's language model: {e.Message}", e);
            }
        }
    }

    // The model tools of an ask; tests replace them.
    internal Func<Task<Result<IAgentTools>>>? ConnectTools { get; init; }

    async Task<VsCodeTurnResult> AskTurnAsync(VsCodeTurnParams turn)
    {
        using CancellationTokenSource cts = new(askTimeout);
        return await server.SendRequest(VsCodeTurnParams.Method, turn).Returning<VsCodeTurnResult>(cts.Token);
    }
}
