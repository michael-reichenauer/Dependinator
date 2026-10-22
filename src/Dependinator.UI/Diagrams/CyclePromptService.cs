using Dependinator.Core.Shared;
using Dependinator.UI.Modeling.Models;

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
        var sources = await ReadAsync(requests);

        using (var model = modelMgr.UseModel())
            return CyclePrompt.Build(model, cycle, sources);
    }

    public string GetFixPrompt(Cycle cycle, CycleSuggestion suggestion)
    {
        using var model = modelMgr.UseModel();
        return CyclePrompt.BuildFix(model, cycle, suggestion);
    }

    // The web host and the VS Code extension see the workspace files; the standalone browser
    // app has none.
    static bool CanReadSource => Dependinator.Core.Build.IsWeb || Dependinator.Core.Build.IsVsCodeExtWasm;

    // Each excerpt once (two types in a short file both show all of it), under its first direction.
    async Task<IReadOnlyList<SourceSection>> ReadAsync(IReadOnlyList<SourceRequest> requests)
    {
        List<SourceSection> sections = [];
        HashSet<(string, int)> shown = [];
        foreach (var request in requests)
        {
            List<SourceExcerpt> excerpts = [];
            foreach (var unit in request.Units)
            {
                var text = await workspaceFileService.ReadSourceFileAsync(unit.Span.Path);
                if (text is not string fileText)
                {
                    Log.Info("No source for the cycle prompt", text.Error.Message);
                    continue;
                }
                excerpts.AddRange(SourceExcerpts.Cut(unit, fileText).Where(e => shown.Add((e.Path, e.StartLine))));
            }
            if (excerpts.Count > 0)
                sections.Add(new SourceSection(request.Direction, excerpts));
        }
        return sections;
    }
}
