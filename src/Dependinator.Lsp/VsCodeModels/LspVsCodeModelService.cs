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

public record VsCodeOpenChatParams(string Prompt)
{
    public static readonly string Method = "dependinator/lm/openChat";
}

public record VsCodeOpenChatResult(string? Error);

// The UI's IVsCodeModelService in VS Code: each call is a request to the extension.
class LspVsCodeModelService(ILanguageServerFacade server, IModelToolsServer modelToolsServer) : IVsCodeModelService
{
    static readonly TimeSpan modelsTimeout = TimeSpan.FromSeconds(10);

    // The extension gives up after 5 minutes; VS Code's consent dialog may wait on the user first.
    static readonly TimeSpan askTimeout = TimeSpan.FromMinutes(10);

    // A whole ask with the model tools, all its turns.
    static readonly TimeSpan toolsAskTimeout = TimeSpan.FromMinutes(15);

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
        var connected = await (ConnectTools ?? ConnectModelToolsAsync)();
        if (connected is not IAgentTools tools)
            return connected.Error;
        await using (tools)
        {
            // All turns together: a slow model that keeps looking things up stops at some point.
            using CancellationTokenSource total = new(toolsAskTimeout);
            try
            {
                return await VsCodeToolsAgent.AskAsync(
                    turn => AskTurnAsync(turn, total.Token),
                    modelId,
                    prompt,
                    jsonSchema,
                    tools
                );
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

    public async Task<Result> OpenAgentChatAsync(string prompt)
    {
        try
        {
            using CancellationTokenSource cts = new(modelsTimeout);
            var result = await server
                .SendRequest(VsCodeOpenChatParams.Method, new VsCodeOpenChatParams(prompt))
                .Returning<VsCodeOpenChatResult>(cts.Token);
            return result.Error is { Length: > 0 } error ? new Error(error) : Result.Ok;
        }
        catch (OperationCanceledException)
        {
            return new Error("VS Code did not open Copilot Chat in time.");
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            return new Error($"Could not open Copilot Chat: {e.Message}", e);
        }
    }

    // The model tools of an ask; tests replace them.
    internal Func<Task<Result<IAgentTools>>>? ConnectTools { get; init; }

    async Task<Result<IAgentTools>> ConnectModelToolsAsync() =>
        modelToolsServer.Address is { } address
            ? await McpAgentTools.ConnectAsync(address)
            : new Error("The model tools are not ready yet. Please try again in a moment.");

    async Task<VsCodeTurnResult> AskTurnAsync(VsCodeTurnParams turn, CancellationToken total)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(total);
        cts.CancelAfter(askTimeout);
        return await server.SendRequest(VsCodeTurnParams.Method, turn).Returning<VsCodeTurnResult>(cts.Token);
    }
}
