using Dependinator.Core.Claude;
using Dependinator.Core.VsCodeModels;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams.Ai;

// What a review dialog says while an answer is on its way. In VS Code the key is entered in an
// input box at the top of the window, outside the UI the user is watching, so the dialog says so.
public enum SuggestStep
{
    EnteringApiKey,
    EnteringNewApiKey,
    AskingClaude,
    AskingClaudeCode,
    AskingCopilot,
}

interface IAiService
{
    // Whether this host can ask Claude: the web host and VS Code, not the standalone browser app.
    bool IsAvailable { get; }

    // Whether the user turned on the experimental Claude Code route (Settings › AI).
    Task<bool> IsClaudeCodeEnabledAsync();

    // Turning Claude Code off also clears it as the default, so no default comes back by itself.
    Task SetClaudeCodeEnabledAsync(bool isEnabled);

    // The providers the user can ask now, and their default (Settings › AI).
    Task<AiChoice> GetChoiceAsync();

    Task SetDefaultAsync(AiSource source);

    // The models VS Code offers the Copilot provider (none outside VS Code), and the one it asks
    // (Config.CopilotModelId; null: Copilot's first model). isFresh skips the cached list.
    Task<VsCodeModel[]> GetCopilotModelsAsync(bool isFresh = false);

    Task<string?> GetCopilotModelIdAsync();

    Task SetCopilotModelAsync(string? modelId);

    // The models the API key can ask (IClaudeService.GetModelsAsync; none without a key, which
    // is never asked for here), and the one picked in Settings › AI (Config.ClaudeModelId; null:
    // the app picks per task). isFresh skips the cached list.
    Task<ClaudeModel[]> GetClaudeModelsAsync(bool isFresh = false);

    Task<string?> GetClaudeModelIdAsync();

    Task SetClaudeModelAsync(string? modelId);

    // The model Claude Code is started with (Config.ClaudeCodeModel, an alias or a full name;
    // null: Claude Code's own setting).
    Task<string?> GetClaudeCodeModelAsync();

    Task SetClaudeCodeModelAsync(string? model);

    // Raised when Settings › AI changes the providers or the default.
    event Action? ChoiceChanged;

    // Asks Claude (with the API key) or Claude Code for an answer matching the schema. Without a
    // key, or with one Claude did not accept, the user is asked for a key first (once per call);
    // onStep tells the dialog what is going on. The prompt is built only once it is needed.
    // Claude answers on the model picked in Settings › AI; without a pick a quick question goes
    // to its faster, cheaper model. Claude Code uses the model picked for it, or the user's own.
    // withModelTools lets Claude Code ask the open model through Dependinator's MCP tools too.
    // Copilot asks the VS Code model picked in Settings › AI, the same for a quick question, with
    // the model tools too where withModelTools asks for them (the LSP's tool loop).
    Task<Result<string>> AskAsync(
        AiSource route,
        Func<Task<string>> getPrompt,
        string jsonSchema,
        Action<SuggestStep> onStep,
        bool isQuick = false,
        bool withModelTools = false
    );

    // Asks Claude with the API key (IClaudeService.AskWithModelToolsAsync; the key is handled as
    // for AskAsync) or Copilot (IVsCodeModelService.AskWithModelToolsAsync), letting it look things
    // up first with Dependinator's model tools.
    Task<Result<ToolsAnswer>> AskWithModelToolsAsync(
        AiSource route,
        Func<Task<string>> getPrompt,
        string jsonSchema,
        Action<SuggestStep> onStep
    );

    // Whether "Fix with Claude Code" can run: the route is on and the model has a folder on disk.
    Task<bool> CanFixWithClaudeCodeAsync();

    // The number of files with uncommitted changes in the model's git repository; an error when
    // the folder is not in one.
    Task<Result<int>> CountUncommittedChangesAsync();

    // Lets Claude Code change the code in the model's folder as the fix prompt asks (no commands).
    Task<Result<ClaudeCodeFixReport>> FixWithClaudeCodeAsync(string fixPrompt);

    Task StopClaudeCodeFixAsync();

    // Hands the fix prompt to Copilot Chat in agent mode (IVsCodeModelService.OpenAgentChatAsync),
    // which makes the change with its own tools and review; the extension re-parses after.
    Task<Result> FixWithCopilotAsync(string fixPrompt);
}

// The AI routes the review dialogs share, over Core's IClaudeService (the LSP or the web host) and,
// in VS Code, IVsCodeModelService (the LSP asks the extension).
[Scoped]
class AiService(
    IClaudeService claudeService,
    IVsCodeModelService vsCodeModelService,
    IConfigService configService,
    IModelMgr modelMgr
) : IAiService
{
    public bool IsAvailable => Dependinator.Core.Build.IsWeb || Dependinator.Core.Build.IsVsCodeExtWasm;

    // A prompt builder returns nothing when what it is about is gone (a re-parse while the dialog
    // is open); nothing is sent then.
    public const string EmptyPromptError =
        "There is no prompt: what it is about is no longer in the model. Close the dialog and review it again.";

    public async Task<bool> IsClaudeCodeEnabledAsync() => IsAvailable && (await configService.GetAsync()).UseClaudeCode;

    public event Action? ChoiceChanged;

    public async Task SetClaudeCodeEnabledAsync(bool isEnabled)
    {
        await configService.SetAsync(config =>
        {
            config.UseClaudeCode = isEnabled;
            if (!isEnabled && config.DefaultAiProvider is AiSource.ClaudeCode)
                config.DefaultAiProvider = null;
        });
        ChoiceChanged?.Invoke();
    }

    public async Task<AiChoice> GetChoiceAsync()
    {
        if (!IsAvailable)
            return AiChoice.ChatOnly;
        var config = await configService.GetAsync();
        // Offered with a model of Copilot's own, or one the user picked from another extension;
        // other extensions also list placeholders that cannot answer (Foundry Toolkit's).
        var models = await GetCopilotModelsAsync();
        var hasCopilot = models.Any(m => m.IsCopilots || m.Id == config.CopilotModelId);
        List<AiProvider> providers =
        [
            .. AiProvider.All.Where(p =>
                p.Source switch
                {
                    AiSource.ClaudeCode => config.UseClaudeCode,
                    AiSource.Copilot => hasCopilot,
                    _ => true,
                }
            ),
        ];
        // A Copilot default stays while VS Code lists no model for a moment (Copilot still starting
        // or signing in), so the first ask does not take another provider as the new default.
        var isOffered =
            config.DefaultAiProvider is AiSource.Copilot && Dependinator.Core.Build.IsVsCodeExtWasm
            || providers.Any(p => p.Source == config.DefaultAiProvider);
        return new AiChoice(providers, isOffered ? config.DefaultAiProvider : null);
    }

    public async Task SetDefaultAsync(AiSource source)
    {
        await configService.SetAsync(config => config.DefaultAiProvider = source);
        ChoiceChanged?.Invoke();
    }

    // The list crosses the webview, the LSP and the extension, and every dialog that asks an AI
    // needs it, so it is kept a minute (an empty one a few seconds: Copilot may be starting).
    static readonly TimeSpan CopilotModelsTtl = TimeSpan.FromMinutes(1);
    static readonly TimeSpan NoCopilotModelsTtl = TimeSpan.FromSeconds(5);
    Task<VsCodeModel[]>? copilotModels;
    DateTime copilotModelsTime;

    public async Task<VsCodeModel[]> GetCopilotModelsAsync(bool isFresh = false)
    {
        if (!Dependinator.Core.Build.IsVsCodeExtWasm)
            return [];
        if (!isFresh && copilotModels is { } cached)
        {
            var ttl = cached.IsCompleted && cached.Result.Length == 0 ? NoCopilotModelsTtl : CopilotModelsTtl;
            if (!cached.IsCompleted || DateTime.UtcNow - copilotModelsTime < ttl)
                return await cached;
        }
        copilotModelsTime = DateTime.UtcNow;
        copilotModels = ListCopilotModelsAsync();
        return await copilotModels;
    }

    async Task<VsCodeModel[]> ListCopilotModelsAsync()
    {
        try
        {
            return await vsCodeModelService.GetModelsAsync();
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Warn($"Could not list VS Code's language models: {e.Message}");
            return [];
        }
    }

    public async Task<string?> GetCopilotModelIdAsync() => (await configService.GetAsync()).CopilotModelId;

    public async Task SetCopilotModelAsync(string? modelId)
    {
        await configService.SetAsync(config => config.CopilotModelId = modelId);
        ChoiceChanged?.Invoke();
    }

    // Kept like the Copilot list; an empty one (no key yet, or the list failed) only a few seconds.
    Task<ClaudeModel[]>? claudeModels;
    DateTime claudeModelsTime;

    // What the e2e tests pick from, where no key is set.
    static readonly ClaudeModel[] TestModeClaudeModels =
    [
        new("claude-opus-5-5", "Claude Opus 5.5"),
        new("claude-sonnet-5-5", "Claude Sonnet 5.5"),
    ];

    public async Task<ClaudeModel[]> GetClaudeModelsAsync(bool isFresh = false)
    {
        if (!IsAvailable)
            return [];
        if (!isFresh && claudeModels is { } cached)
        {
            var ttl = cached.IsCompleted && cached.Result.Length == 0 ? NoCopilotModelsTtl : CopilotModelsTtl;
            if (!cached.IsCompleted || DateTime.UtcNow - claudeModelsTime < ttl)
                return await cached;
        }
        claudeModelsTime = DateTime.UtcNow;
        claudeModels = ListClaudeModelsAsync();
        return await claudeModels;
    }

    async Task<ClaudeModel[]> ListClaudeModelsAsync()
    {
        try
        {
            if (await claudeService.GetApiKeyStateAsync() is not ClaudeApiKeyState.Set)
                return Dependinator.Core.Build.IsTestMode ? TestModeClaudeModels : [];
            var models = await claudeService.GetModelsAsync();
            if (models is ClaudeModel[] listed)
                return listed;
            Log.Warn($"Could not list Claude's models: {models.Error.Message}");
            return [];
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Warn($"Could not list Claude's models: {e.Message}");
            return [];
        }
    }

    public async Task<string?> GetClaudeModelIdAsync() => (await configService.GetAsync()).ClaudeModelId;

    public async Task SetClaudeModelAsync(string? modelId)
    {
        await configService.SetAsync(config => config.ClaudeModelId = modelId);
        ChoiceChanged?.Invoke();
    }

    public async Task<string?> GetClaudeCodeModelAsync() => (await configService.GetAsync()).ClaudeCodeModel;

    public async Task SetClaudeCodeModelAsync(string? model)
    {
        await configService.SetAsync(config => config.ClaudeCodeModel = model);
        ChoiceChanged?.Invoke();
    }

    public async Task<Result<string>> AskAsync(
        AiSource route,
        Func<Task<string>> getPrompt,
        string jsonSchema,
        Action<SuggestStep> onStep,
        bool isQuick = false,
        bool withModelTools = false
    )
    {
        if (route is AiSource.ClaudeCode)
        {
            onStep(SuggestStep.AskingClaudeCode);
            var codePrompt = await getPrompt();
            if (codePrompt == "")
                return new Error(EmptyPromptError);
            try
            {
                return await claudeService.AskClaudeCodeAsync(
                    codePrompt,
                    jsonSchema,
                    CodeFolder(),
                    withModelTools,
                    await GetClaudeCodeModelAsync()
                );
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                return new Error($"Could not ask Claude Code: {e.Message}", e);
            }
        }

        if (route is AiSource.Copilot && withModelTools)
        {
            var toolsAnswer = await AskWithModelToolsAsync(route, getPrompt, jsonSchema, onStep);
            return toolsAnswer is ToolsAnswer answer ? answer.Json : toolsAnswer.Error;
        }
        if (route is AiSource.Copilot)
            return await AskCopilotAsync(
                getPrompt,
                onStep,
                (prompt, modelId) => vsCodeModelService.AskAsync(prompt, jsonSchema, modelId)
            );

        return await WithApiKeyAsync(
            getPrompt,
            onStep,
            async prompt =>
            {
                var modelId = await GetClaudeModelIdAsync();
                return isQuick
                    ? await claudeService.AskQuicklyAsync(prompt, jsonSchema, modelId)
                    : await claudeService.AskAsync(prompt, jsonSchema, modelId);
            }
        );
    }

    public async Task<Result<ToolsAnswer>> AskWithModelToolsAsync(
        AiSource route,
        Func<Task<string>> getPrompt,
        string jsonSchema,
        Action<SuggestStep> onStep
    )
    {
        return route switch
        {
            AiSource.Claude => await WithApiKeyAsync(
                getPrompt,
                onStep,
                async prompt =>
                    await claudeService.AskWithModelToolsAsync(prompt, jsonSchema, await GetClaudeModelIdAsync())
            ),
            AiSource.Copilot => await AskCopilotAsync(
                getPrompt,
                onStep,
                (prompt, modelId) => vsCodeModelService.AskWithModelToolsAsync(prompt, jsonSchema, modelId)
            ),
            _ => new Error($"{route.Name()} cannot look things up with the model tools here."),
        };
    }

    // Copilot asks the VS Code model picked in Settings › AI (IVsCodeModelService, in the LSP).
    async Task<Result<T>> AskCopilotAsync<T>(
        Func<Task<string>> getPrompt,
        Action<SuggestStep> onStep,
        Func<string, string?, Task<Result<T>>> ask
    )
        where T : notnull
    {
        onStep(SuggestStep.AskingCopilot);
        var prompt = await getPrompt();
        if (prompt == "")
            return new Error(EmptyPromptError);
        try
        {
            return await ask(prompt, await GetCopilotModelIdAsync());
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            return new Error($"Could not ask Copilot: {e.Message}", e);
        }
    }

    // Without a key, or with one Claude did not accept, the user is asked for a key first (once).
    async Task<Result<T>> WithApiKeyAsync<T>(
        Func<Task<string>> getPrompt,
        Action<SuggestStep> onStep,
        Func<string, Task<Result<T>>> ask
    )
        where T : notnull
    {
        var hasAskedForKey = false;
        string? prompt = null;
        while (true)
        {
            var state = await claudeService.GetApiKeyStateAsync();
            if (state is not ClaudeApiKeyState.Set)
            {
                onStep(state is ClaudeApiKeyState.Missing ? SuggestStep.EnteringApiKey : SuggestStep.EnteringNewApiKey);
                if (await claudeService.AskUserForApiKeyAsync() is Error e)
                    return e;
                hasAskedForKey = true;
            }

            onStep(SuggestStep.AskingClaude);
            prompt ??= await getPrompt();
            if (prompt == "")
                return new Error(EmptyPromptError);
            var answer = await ask(prompt);
            if (answer.Value is T)
                return answer;
            // A key Claude did not accept: ask for a new one, once.
            if (hasAskedForKey || await claudeService.GetApiKeyStateAsync() is not ClaudeApiKeyState.Rejected)
                return answer.Error;
        }
    }

    // Copilot is offered only in VS Code (GetCopilotModelsAsync lists no models elsewhere).
    // Said where a prompt was handed to Copilot Chat: nothing comes back to the dialog.
    public const string CopilotFixNote =
        "The prompt is in Copilot Chat, in agent mode: it makes the change there, and you keep or undo each edit. "
        + "Once the edits are kept, the diagram re-parses (or Re-parse shows a dot, with auto refresh off).";

    public async Task<Result> FixWithCopilotAsync(string fixPrompt)
    {
        if (fixPrompt == "")
            return new Error(EmptyPromptError);
        try
        {
            return await vsCodeModelService.OpenAgentChatAsync(fixPrompt);
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            return new Error($"Could not open Copilot Chat: {e.Message}", e);
        }
    }

    public async Task<bool> CanFixWithClaudeCodeAsync() =>
        await IsClaudeCodeEnabledAsync() && (CodeFolder() is not null || Dependinator.Core.Build.IsTestMode);

    public async Task<Result<int>> CountUncommittedChangesAsync()
    {
        if (Dependinator.Core.Build.IsTestMode)
            return 0;
        if (CodeFolder() is not { } folder)
            return new Error("The model has no folder on disk.");
        try
        {
            return await claudeService.CountUncommittedChangesAsync(folder);
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            return new Error($"Could not check for uncommitted changes: {e.Message}", e);
        }
    }

    public async Task<Result<ClaudeCodeFixReport>> FixWithClaudeCodeAsync(string fixPrompt)
    {
        // The e2e tests exercise the dialog without Claude Code: a canned report after a moment.
        if (Dependinator.Core.Build.IsTestMode)
        {
            using var stop = testModeStop = new();
            try
            {
                await Task.Delay(TestModeFixDelay, stop.Token);
                return new ClaudeCodeFixReport("Test mode: Claude Code was not run.", ["src/Test.cs"]);
            }
            catch (OperationCanceledException)
            {
                return new Error("Claude Code was stopped.");
            }
            finally
            {
                testModeStop = null;
            }
        }
        if (fixPrompt == "")
            return new Error(EmptyPromptError);
        if (CodeFolder() is not { } folder)
            return new Error("The model has no folder on disk for Claude Code to change.");
        try
        {
            var report = await claudeService.FixWithClaudeCodeAsync(
                fixPrompt,
                ClaudeCodeFixReport.JsonSchema,
                folder,
                await GetClaudeCodeModelAsync()
            );
            return report is string json ? ClaudeCodeFixReport.Parse(json, folder) : report.Error;
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            return new Error($"Could not run Claude Code: {e.Message}", e);
        }
    }

    static readonly TimeSpan TestModeFixDelay = TimeSpan.FromMilliseconds(2500);
    CancellationTokenSource? testModeStop;

    public async Task StopClaudeCodeFixAsync()
    {
        if (Dependinator.Core.Build.IsTestMode)
        {
            Result.Catch(() => testModeStop?.Cancel());
            return;
        }
        try
        {
            await claudeService.StopClaudeCodeFixAsync();
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Info("Could not stop Claude Code", e.Message);
        }
    }

    // The model's folder (the solution's or the Bicep file's), where Claude Code may read the
    // code; none for a model without a file, such as the demo. The UI runs in the browser, so
    // System.IO.Path would read a Windows path by Unix rules: both separators are cut by hand,
    // and the host checks that the folder is there.
    string? CodeFolder() => FolderOf(modelMgr.WithModel(m => m.Path));

    internal static string? FolderOf(string path)
    {
        if (path == "" || !(path.StartsWith('/') || (path.Length > 2 && path[1] == ':')))
            return null;
        var cut = path.TrimEnd('/', '\\').LastIndexOfAny(['/', '\\']);
        return cut > 0 ? path[..cut] : null;
    }
}
