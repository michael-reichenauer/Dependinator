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
                return Saved(saved, key) is { } source
                    ? new SourceChoice(path, key, source.IsIncluded)
                    : new SourceChoice(path, key, WorkspaceSources.IsProposed(key), IsNew: true);
            }),
        ];

    // The choices as saved with the model. A source keeps the root node it had (see
    // WorkspaceSource.RootName) unless the parse gave it another. Whether it is in the model
    // is what a refresh says (inModelPaths: the sources it parsed or kept), and stays as it
    // was otherwise. A saved source that is not found now is kept: it can be back (another
    // branch was checked out for a while), and what the user chose about it, and the names of
    // its nodes, should hold then.
    public static IReadOnlyList<WorkspaceSource> ToSaved(
        IReadOnlyList<SourceChoice> choices,
        IReadOnlyList<WorkspaceSource> saved,
        IReadOnlyDictionary<string, string>? parsedRootNames = null,
        IReadOnlyCollection<string>? inModelPaths = null
    )
    {
        // A root name that a parsed source has now is no longer the name of a source that was
        // not parsed: that one had it while the other was not found, and would claim it back.
        var parsedNames = parsedRootNames?.Values.ToHashSet(StringComparer.Ordinal) ?? [];
        string? Kept(string? rootName) => rootName is not null && parsedNames.Contains(rootName) ? null : rootName;

        return
        [
            .. choices.Select(choice => new WorkspaceSource(
                choice.Key,
                choice.IsIncluded,
                parsedRootNames?.GetValueOrDefault(choice.Path) ?? Kept(Saved(saved, choice.Key)?.RootName),
                inModelPaths?.Contains(choice.Path) ?? Saved(saved, choice.Key)?.IsInModel ?? false
            )),
            .. saved
                .Where(source =>
                    !choices.Any(choice => string.Equals(choice.Key, source.Path, StringComparison.OrdinalIgnoreCase))
                )
                .Select(source =>
                    source with
                    {
                        RootName = Kept(source.RootName),
                        IsInModel = inModelPaths is null && source.IsInModel,
                    }
                ),
        ];
    }

    // What is saved about the source with the key, if anything.
    public static WorkspaceSource? Saved(IReadOnlyList<WorkspaceSource> saved, string key) =>
        saved.FirstOrDefault(source => string.Equals(source.Path, key, StringComparison.OrdinalIgnoreCase));
}
