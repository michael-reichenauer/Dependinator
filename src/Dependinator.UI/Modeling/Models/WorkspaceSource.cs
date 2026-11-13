namespace Dependinator.UI.Modeling.Models;

// One of the sources found in the workspace of a workspace model (a solution, a Bicep deployment,
// a script project), and whether the user chose it to be in the model. Saved with the model.
// Path is the source's key: its path below the workspace folder (Core's WorkspaceSources.Key),
// so the choice holds wherever the folder is checked out. RootName is the root node of the
// source's nodes as of its last parse (null before it): what the model keeps when a later parse
// of the source fails.
record WorkspaceSource(string Path, bool IsIncluded, string? RootName = null);
