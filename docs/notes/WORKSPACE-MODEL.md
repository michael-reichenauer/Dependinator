# One model for the workspace

A workspace model shows several of a workspace's sources (solutions, Bicep deployments,
TypeScript and JavaScript projects) in one diagram, each parsed by its own parser. In this
repository that is the solution, the VS Code extension and the TypeScript parser's helper side by
side. The design with its alternatives is https://claude.ai/artifact/6f9JbUbR7w2yT13DngeXdu
(Nov 12 2026); Michael took all five recommendations (Nov 13). Phase 1 is built.

## What the user sees

- The Models menu and the Models dialog list the workspace model first under "This workspace",
  named after the workspace folder with "whole workspace" beside it. It is there only when the
  workspace has at least two sources; with one it would be that source's own model.
- The first time it is opened, "What is in this model?" lists the found sources by kind with a
  checkbox each. The proposal ticks everything that is not in a folder named `test`, `tests`,
  `sample`, `samples`, `example` or `examples`. Cancel leaves the open model as it was; with no
  model open yet (the app is starting on it) the proposal is used.
- "Choose Sources …" (Models menu and Settings) changes the choice later and parses again. A
  source found since the choice was saved is marked "new" and follows the proposal rule.
- The diagram has one root node per source and one Externals. A source that cannot be parsed is
  a problem in the problem indicator, and its part of the diagram stays as it was.
- The model is beside the per-source models, not instead of them, and a host does not start on
  it by itself: once opened it is the last used model like any other.

## How it is built

- **The path.** A workspace model has no file. Its path is `<workspace folder>/.workspace`
  (`ModelPaths.WorkspacePath`, `IsWorkspace`), so the saved model, the recent list and cloud sync
  key on it like on any path. `ModelPaths.IsSource` is true for what a parser reads, and
  `IsParseable` for a source or a workspace path.
- **The sources** are what `IWorkspaceFileService.GetModelFilePathsAsync` finds.
  `ModelListService` keeps them (`WorkspaceSourcePaths`, `WorkspaceFolder` from the new
  `GetWorkspaceFoldersAsync`, `WorkspaceModelPath`) and lists them again before the dialog
  opens. The workspace model is last in `LocalPaths`, so the model a new workspace starts on
  stays a single source.
- **The choice** is saved with the model: `Model.WorkspaceSources` (`ModelDto.WorkspaceSources`,
  no format bump), one `WorkspaceSource` per source with its key (the path below the workspace
  folder with "/", so it holds wherever the folder is checked out), whether it is included, and
  the name of its root node from the last parse. `SourceChoices.Of` joins the found sources with
  the saved ones; `WorkspaceSources` (Core) has the key and the proposal rule.
- **The parse.** `ModelService` sends the included sources on `SolutionParseOptions.Sources`
  (the parse runs in the LSP in VS Code). `ParserService.ParseWorkspaceAsync` gives each source
  to its own parser: different parsers run at the same time, the sources of one parser one by
  one (MSBuild allows one build in a process). `WorkspaceItems.Combine` makes one item list:
  - node names are the ones each source's own model has, so source files, rules and searches
    hold in both;
  - a source whose root is named like an earlier node gets its folder in the root's name
    (`app (services∕app)`), and so in all its node names;
  - a node an earlier source already has is left to that source (a project shared by two
    solutions is shown once);
  - a failed source is a `ParseProblem`; only when every source fails is the parse an error;
  - each parsed source adds a `ParsedSource` item with its root name.
- **A failed source keeps its nodes.** `StructureService.ClearNotUpdated` removes what the
  parse did not stamp, so `ModelService.UpdateWorkspaceSources` re-stamps the subtree of an
  included source that has no `ParsedSource` item (`StructureService.KeepNodeTree`, by the saved
  root name).
- **The UI.** `CanvasService.LoadAsync` asks `IWorkspaceSourcesService.AskBeforeFirstOpenAsync`
  before loading (a workspace model with no saved model), and passes the answer to
  `ModelService.LoadAsync`. `WorkspaceSourcesDialog` is the dialog; `ChooseAsync` is behind the
  menu item and the Settings button and ends in `ModelService.SetSourceChoicesAsync`.
- A workspace model of another folder (synced from another device) opens as it was saved and
  cannot be parsed here: its sources are that folder's.

## Left for later

- **Phase 2:** parse only the sources whose kind of file changed (the extension already names
  the kinds), and notice a new source without a restart. Today every refresh parses every
  included source, about as long as the slowest one since the parsers run side by side.
- **Phase 3 (optional):** bring the layout along from a source's own model when it is added.
- **Phase 4:** let VS Code start on the workspace model (a setting or a question), once it has
  been lived with.
- Links between languages (the extension starting the language server) are not made: each
  parser sees only its own source.
- A workspace with several folders uses the first as the workspace folder; sources of the others
  keep their whole path as key.
