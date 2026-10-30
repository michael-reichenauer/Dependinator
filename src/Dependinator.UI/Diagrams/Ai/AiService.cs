using Dependinator.Core.Claude;
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

    // Raised when Settings › AI changes the providers or the default.
    event Action? ChoiceChanged;

    // Asks Claude (with the API key) or Claude Code for an answer matching the schema. Without a
    // key, or with one Claude did not accept, the user is asked for a key first (once per call);
    // onStep tells the dialog what is going on. The prompt is built only once it is needed. A
    // quick question goes to Claude's faster, cheaper model (Claude Code uses the user's own).
    // withModelTools lets Claude Code ask the open model through Dependinator's MCP tools too.
    Task<Result<string>> AskAsync(
        AiSource route,
        Func<Task<string>> getPrompt,
        string jsonSchema,
        Action<SuggestStep> onStep,
        bool isQuick = false,
        bool withModelTools = false
    );

    // Asks Claude with the API key, letting it look things up first with Dependinator's model tools
    // (IClaudeService.AskWithModelToolsAsync); the key is handled as for AskAsync.
    Task<Result<ToolsAnswer>> AskWithModelToolsAsync(
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
}

// The AI routes the review dialogs share, over Core's IClaudeService (the LSP or the web host).
[Scoped]
class AiService(IClaudeService claudeService, IConfigService configService, IModelMgr modelMgr) : IAiService
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
        List<AiProvider> providers = [.. AiProvider.All.Where(p => p != AiProvider.ClaudeCode || config.UseClaudeCode)];
        var isOffered = providers.Any(p => p.Source == config.DefaultAiProvider);
        return new AiChoice(providers, isOffered ? config.DefaultAiProvider : null);
    }

    public async Task SetDefaultAsync(AiSource source)
    {
        await configService.SetAsync(config => config.DefaultAiProvider = source);
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
                return await claudeService.AskClaudeCodeAsync(codePrompt, jsonSchema, CodeFolder(), withModelTools);
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                return new Error($"Could not ask Claude Code: {e.Message}", e);
            }
        }

        return await WithApiKeyAsync(
            getPrompt,
            onStep,
            prompt =>
                isQuick ? claudeService.AskQuicklyAsync(prompt, jsonSchema) : claudeService.AskAsync(prompt, jsonSchema)
        );
    }

    public Task<Result<ToolsAnswer>> AskWithModelToolsAsync(
        Func<Task<string>> getPrompt,
        string jsonSchema,
        Action<SuggestStep> onStep
    ) => WithApiKeyAsync(getPrompt, onStep, prompt => claudeService.AskWithModelToolsAsync(prompt, jsonSchema));

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
            var report = await claudeService.FixWithClaudeCodeAsync(fixPrompt, ClaudeCodeFixReport.JsonSchema, folder);
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
