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

    // Whether the user turned on the experimental Claude Code route (Settings › Use Claude Code).
    Task<bool> IsClaudeCodeEnabledAsync();

    // Asks Claude (with the API key) or Claude Code for an answer matching the schema. Without a
    // key, or with one Claude did not accept, the user is asked for a key first (once per call);
    // onStep tells the dialog what is going on. The prompt is built only once it is needed.
    Task<Result<string>> AskAsync(
        AiSource route,
        Func<Task<string>> getPrompt,
        string jsonSchema,
        Action<SuggestStep> onStep
    );
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

    public async Task<Result<string>> AskAsync(
        AiSource route,
        Func<Task<string>> getPrompt,
        string jsonSchema,
        Action<SuggestStep> onStep
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
                return await claudeService.AskClaudeCodeAsync(codePrompt, jsonSchema, CodeFolder());
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                return new Error($"Could not ask Claude Code: {e.Message}", e);
            }
        }

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
            var answer = await claudeService.AskAsync(prompt, jsonSchema);
            if (answer is string)
                return answer;
            // A key Claude did not accept: ask for a new one, once.
            if (hasAskedForKey || await claudeService.GetApiKeyStateAsync() is not ClaudeApiKeyState.Rejected)
                return answer.Error;
        }
    }

    // The model's folder (the solution's or the Bicep file's), where Claude Code may read the
    // code; none for a model without a file, such as the demo.
    string? CodeFolder()
    {
        var path = modelMgr.WithModel(m => m.Path);
        return Path.IsPathFullyQualified(path) ? Path.GetDirectoryName(path) : null;
    }
}
