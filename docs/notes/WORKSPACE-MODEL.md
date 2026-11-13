# One model for the workspace

A workspace model shows several of a workspace's sources (solutions, Bicep deployments,
TypeScript and JavaScript projects) in one diagram, each parsed by its own parser. In this
repository that is the solution, the VS Code extension and the TypeScript parser's helper side by
side. The design with its alternatives is https://claude.ai/artifact/6f9JbUbR7w2yT13DngeXdu
(Nov 12 2026); Michael took all five recommendations (Nov 13). Phases 1 and 2 are built.

## What the user sees

- The Models menu has the workspace model in a section of its own, "Whole workspace", named
  after the workspace folder, with "Choose Sources …" right below it while it is open; the
  models of the sources follow under "Single sources" (Michael's idea, Nov 13). The Models
  dialog lists it first under "This workspace" with "whole workspace" beside it. It is there
  only when the workspace has at least two sources; with one it would be that source's own
  model (and the menu has its one list, "This workspace"). A model that is open when the
  workspace gets down to one source stays what it is: it still refreshes and its sources can
  be chosen, it is only no longer offered in the list.
- The first time it is opened, "What is in this model?" lists the found sources by kind with a
  checkbox each. The proposal ticks everything that is not in a folder named `test`, `tests`,
  `sample`, `samples`, `example` or `examples`. Cancel leaves the open model as it was; with no
  model open yet (the app is starting on it) the proposal is used.
- "Choose Sources …" (below the model in the Models menu, and in Settings) changes the choice
  later and parses again. A source found since the choice was saved is marked "new" and
  follows the proposal rule. A source that is not found for a while (another branch is checked
  out) keeps its choice.
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
  stays a single source. `WorkspaceModelPath` says which path is this workspace's model
  (whenever there is a workspace folder, compared with `ModelListService.IsSamePath`, since a
  recent or cloud path can spell it another way). It is listed with two or more sources;
  listed or not it is a workspace path, and a local path (so it refreshes) while there is a
  source. With no source left it is a visit that shows what was saved.
- **The choice** is saved with the model: `Model.WorkspaceSources` (`ModelDto.WorkspaceSources`,
  no format bump), one `WorkspaceSource` per source with its key (the path below the workspace
  folder with "/", so it holds wherever the folder is checked out), whether it is included, and
  the name of its root node from the last parse. `SourceChoices.Of` joins the found sources with
  the saved ones; `WorkspaceSources` (Core) has the key and the proposal rule. The choice is
  saved only once the model has been parsed: until then `ModelService` holds it
  (`unparsedSourceChoices`), so after a failed first parse Retry is of the same sources without
  asking, and opening the model again shows the dialog with what the user chose. A saved
  source that is not found now is kept (it can be back with another branch); such entries are
  never removed, they are small.
- **The parse.** `ModelService` sends the included sources on `SolutionParseOptions.Sources`
  (the parse runs in the LSP in VS Code). `ParserService.ParseWorkspaceAsync` gives each source
  to its own parser: different parsers run at the same time, the sources of one parser one by
  one (MSBuild allows one build in a process). `WorkspaceItems.Combine` makes one item list:
  - node names are the ones each source's own model has, so source files, rules and searches
    hold in both;
  - a source whose root is named like an earlier node gets its folder in the root's name
    (`app (services∕app)`), and so in all its node names;
  - a root keeps the name it had in its last parse (`SolutionParseOptions.KnownSources`, from
    the saved root names), whichever of the other sources parse this time and in whatever
    order, since the layout, the rules and a failed source's kept nodes all go by name. The
    name is held for its source also while that fails or is left out of the model, so a name
    once given stays, and a source added later is the one that is renamed. Only a source whose
    root has another name now (a package was renamed) gives the old name up. Should two sources
    have had one name (each parsed while the other was not found), one in this parse owns it,
    and the other's saved name is forgotten (`SourceChoices.ToSaved`);
  - a node an earlier source already has is left to that source (a project shared by two
    solutions is shown once). Within one source every item is kept, as in its own model;
  - two C# projects that only share their name (two different `App.Core` in two solutions) are
    shown as one, because a C# project's nodes are not named by its solution. That is reported
    as a problem at the project, which says to leave one of the sources out;
  - a failed source is a `ParseProblem`; only when every source fails is the parse an error;
  - each parsed source adds a `ParsedSource` item with its root name.
- **A refresh parses only what changed (phase 2).** The VS Code extension names the kinds of
  the files that changed (`WorkspaceSources.ChangeKind`: "code" for a solution's files, "bicep",
  "script"), and `ModelService.RefreshChangedAsync` parses only the model's sources of those
  kinds (`PlanSources`); the others keep their nodes (`KeepNodeTree`) and their problems
  (`ParseProblem.Source`, the key of the source a problem is of, set by `WorkspaceItems`). The
  kind is the unit, not the source: two solutions can share a project, and which of them it is
  shown in goes by their order in one parse. A kind is also parsed when the model gets a source
  of it, or loses one (Choose Sources, a source added or deleted): which sources the model has
  is saved with the choice (`WorkspaceSource.IsInModel`, set by every refresh for the sources
  it parsed, also the ones that failed, and the ones it kept). A source that is only taken out
  is removed without a parse, and a change that leaves the model as it is does nothing at all.
  When every source of such a parse fails, that is a problem of each
  (`SolutionParseOptions.HasOtherSources`), not a failed refresh. Parsed in full: the Parse
  button, opening the model, Retry, and a model that does not say which sources it has (saved
  before phase 2, or its only parse failed). Refreshes run one at a time
  (`ModelService.refreshGate`): each plans from the sources the one before left in the model.
  A refresh that waited or parsed while the user opened another model is dropped.
- **A new source is noticed.** The extension adds "sources" to the kinds when a file that can
  make a source is added or removed (a solution, a Bicep file, a script project's config, a
  script file), or when a file that says which files are sources changes: a Bicep file (the
  deployments are the Bicep files no other one uses) or a script project's config (its
  "references"). `VsCodeReceiveService` then lists the sources again
  (`RefreshWorkspaceSourcesAsync`), so the Models menu shows the new model, and a workspace
  model takes a new source in by the proposal rule. The Parse button lists the sources again
  too (`CanvasService.RefreshAsync`): the dev host has no file watcher, and the extension's
  does not see everything (a folder that is moved).
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

- With automatic refresh off in VS Code, the Parse button parses every source: the kinds of
  the changes that were only marked are not kept in the UI.
- A source that failed is parsed again when files of its kind change or with the Parse
  button, not at every refresh (that would parse its whole kind, a second solution too, at
  every change of any file). When the cause is outside the watched files (`npm install`, a
  restore), press Parse.
- A refresh that fails as a whole (the parser is not reached) leaves the model as it was and
  says so with Retry, which parses everything. The kinds it was for are not remembered: a
  later change of another kind parses only that kind.
- Refreshes that wait for each other are not merged into one: only the file watcher's ones
  are (`VsCodeReceiveService`). Pressing Parse three times during a long parse parses three
  more times.
- A model that does not say which sources it has parses everything at any change until one
  parse of it succeeds.
- A refresh can still meet a cloud pull of the same model (`ReplaceCurrentModelAsync`), as
  before phase 2.
- **Phase 3 (optional):** bring the layout along from a source's own model when it is added.
- **Phase 4:** let VS Code start on the workspace model (a setting or a question), once it has
  been lived with.
- Links between languages (the extension starting the language server) are not made: each
  parser sees only its own source.
- A workspace with several folders uses the first as the workspace folder (the editor's order,
  which the LSP's `WorkspaceFolderService` keeps); sources of the others keep their whole path
  as key.
- A project that two solutions share is shown in the first. When the first solution fails as a
  whole (not compile errors, which are problems of a parsed source), the project moves to the
  second until the first parses again: only root names are held for a failed source, not the
  names of its nodes.
- Saved models go by the exact path, for every kind of model. Where the same path is spelled
  two ways (on Windows a cloud copy has "/" where the workspace has "\", and a drive letter's
  case can differ), the workspace model opened by the other spelling is a second saved copy,
  and asks for its sources once more. One canonical path for every model that is opened would
  settle it for all kinds.
- Keeping two same-named C# projects of different solutions apart (the folder in the project's
  name, as for a root) needs the rename to be remembered per project, as it is per root.
