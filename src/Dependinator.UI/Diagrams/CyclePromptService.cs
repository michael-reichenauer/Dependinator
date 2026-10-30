using Dependinator.Core.Shared;
using Dependinator.UI.Modeling.Models;
using static Dependinator.UI.Diagrams.PromptParts;

namespace Dependinator.UI.Diagrams;

interface ICyclePromptService
{
    // The text that asks an AI whether the cycle is intended or should be broken (CyclePrompt),
    // with the source behind its thin directions where the host can read the files.
    Task<string> GetPromptAsync(Cycle cycle);

    // The text that asks an AI that can change the code to break the cycle as suggested.
    string GetFixPrompt(Cycle cycle, CycleSuggestion suggestion);
}

[Scoped]
class CyclePromptService(IModelMgr modelMgr, IWorkspaceFileService workspaceFileService) : ICyclePromptService
{
    public async Task<string> GetPromptAsync(Cycle cycle)
    {
        IReadOnlyList<SourceRequest> requests = [];
        if (CanReadSource)
        {
            using var model = modelMgr.UseModel();
            requests = CyclePrompt.GetSourceRequests(model, cycle);
        }

        // Read without holding the model, since in VS Code the files come from the LSP process.
        var sources = await ReadSourcesAsync(workspaceFileService, requests);

        using (var model = modelMgr.UseModel())
            return CyclePrompt.Build(model, cycle, sources);
    }

    public string GetFixPrompt(Cycle cycle, CycleSuggestion suggestion)
    {
        using var model = modelMgr.UseModel();
        return CyclePrompt.BuildFix(model, cycle, suggestion);
    }
}
