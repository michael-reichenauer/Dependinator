using Dependinator.Core.Shared;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling;

// A source found in the workspace, and whether it is in the workspace model: the user's saved
// choice, or what is proposed for a source the user has not chosen about (IsNew). ShownIn is
// where the user chose to show it (see WorkspaceSource.ShownIn, null for Automatic).
public record SourceChoice(string Path, string Key, bool IsIncluded, bool IsNew = false, string? ShownIn = null);

// The sources of a workspace model as the user chooses them: what is found in the workspace now,
// joined with what the model has saved.
static class SourceChoices
{
    // An .slnx the model has no choice for takes the choice of its .sln when that is gone: the
    // solution was migrated (SlnxMigration).
    public static IReadOnlyList<SourceChoice> Of(
        string workspaceFolder,
        IReadOnlyList<string> foundPaths,
        IReadOnlyList<WorkspaceSource> saved
    )
    {
        var keys = foundPaths.Select(path => WorkspaceSources.Key(workspaceFolder, path)).ToList();
        return
        [
            .. foundPaths.Select(
                (path, i) =>
                    (Saved(saved, keys[i]) ?? MigratedFrom(saved, keys, keys[i])) is { } source
                        ? new SourceChoice(path, keys[i], source.IsIncluded, ShownIn: source.ShownIn)
                        : new SourceChoice(path, keys[i], WorkspaceSources.IsProposed(keys[i]), IsNew: true)
            ),
        ];
    }

    static WorkspaceSource? MigratedFrom(IReadOnlyList<WorkspaceSource> saved, IReadOnlyList<string> keys, string key)
    {
        if (SlnxMigration.SlnPath(key) is not { } slnKey)
            return null;
        if (keys.Any(k => string.Equals(k, slnKey, StringComparison.OrdinalIgnoreCase)))
            return null;
        return Saved(saved, slnKey);
    }

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
                inModelPaths?.Contains(choice.Path) ?? Saved(saved, choice.Key)?.IsInModel ?? false,
                choice.ShownIn
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
