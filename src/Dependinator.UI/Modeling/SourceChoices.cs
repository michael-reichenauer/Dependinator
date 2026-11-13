using Dependinator.Core.Shared;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling;

// A source found in the workspace, and whether it is in the workspace model: the user's saved
// choice, or what is proposed for a source the user has not chosen about (IsNew).
public record SourceChoice(string Path, string Key, bool IsIncluded, bool IsNew = false);

// The sources of a workspace model as the user chooses them: what is found in the workspace now,
// joined with what the model has saved.
static class SourceChoices
{
    public static IReadOnlyList<SourceChoice> Of(
        string workspaceFolder,
        IReadOnlyList<string> foundPaths,
        IReadOnlyList<WorkspaceSource> saved
    ) =>
        [
            .. foundPaths.Select(path =>
            {
                var key = WorkspaceSources.Key(workspaceFolder, path);
                return Find(saved, key) is { } source
                    ? new SourceChoice(path, key, source.IsIncluded)
                    : new SourceChoice(path, key, WorkspaceSources.IsProposed(key), IsNew: true);
            }),
        ];

    // The choices as saved with the model. A source keeps the root node it had (see
    // WorkspaceSource.RootName) unless the parse gave it another; a source that is no longer
    // found is forgotten.
    public static IReadOnlyList<WorkspaceSource> ToSaved(
        IReadOnlyList<SourceChoice> choices,
        IReadOnlyList<WorkspaceSource> saved,
        IReadOnlyDictionary<string, string>? parsedRootNames = null
    ) =>
        [
            .. choices.Select(choice => new WorkspaceSource(
                choice.Key,
                choice.IsIncluded,
                parsedRootNames?.GetValueOrDefault(choice.Path) ?? Find(saved, choice.Key)?.RootName
            )),
        ];

    static WorkspaceSource? Find(IReadOnlyList<WorkspaceSource> saved, string key) =>
        saved.FirstOrDefault(source => string.Equals(source.Path, key, StringComparison.OrdinalIgnoreCase));
}
