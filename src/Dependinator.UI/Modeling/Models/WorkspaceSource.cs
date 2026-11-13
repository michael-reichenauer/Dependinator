namespace Dependinator.UI.Modeling.Models;

// One of the sources found in the workspace of a workspace model (a solution, a Bicep deployment,
// a script project), and whether the user chose it to be in the model. Saved with the model.
// Path is the source's key: its path below the workspace folder (Core's WorkspaceSources.Key),
// so the choice holds wherever the folder is checked out. RootName is the root node of the
// source's nodes as of its last parse (null before it): what the model keeps when a later parse
// of the source fails. IsInModel says that the last refresh had the source in the model, parsed
// then (it may have failed) or kept as it was: a refresh after a change of files parses a kind
// of source only when files of it changed, or the model gets or loses a source of it.
record WorkspaceSource(string Path, bool IsIncluded, string? RootName = null, bool IsInModel = false);
