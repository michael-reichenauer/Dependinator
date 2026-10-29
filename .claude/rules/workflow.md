# Workflow rules and decisions

How this project is worked on, beyond CLAUDE.md. Always loaded, in local and cloud sessions alike.

## Branches, merges and releases

- **Branches** are named after the work, with the `mr/` prefix and a short kebab-case description
  (`mr/release-process`, `mr/fix-diagram-zoom`), branched from `dev`; never generated names.
- **Work on a feature branch and open a pull request into `dev`**; never commit or push to `dev` or
  `main` directly (the one exception: the `release` skill's notes commit). Do not merge the PR
  until the user says so, and link it in the report. After a merge, name the merged branch in the
  report; the user deletes branches (or GitHub deletes head branches).
- **Review before merging into `dev`.** Have the commits to be merged (`dev..<branch>`) reviewed
  in a fresh context first: the `code-review` skill on `<branch>` against `dev`, or a new session.
  Commits that fix review findings count as unreviewed until someone reviews them. Fix what holds
  up, report the findings and get the user's go-ahead before merging. One such review found 15
  issues, among them a cache bug that would have shipped.
- **Merge messages describe the change** as one summary line, then `- ` lines, for users where it
  matters. They are raw material for the release notes, not the notes themselves.
  `git merge --no-ff <branch> -F <file>`; write the message to a file first (`git merge -F -` does
  not read stdin).
- **Releases only through the Release workflow, when the user asks** (the `release` skill,
  `docs/RELEASING.md`): a preview from `dev`, a release from `main`, the notes written in
  `CHANGELOG.md`'s `## [Unreleased]` beforehand. Nothing is published by a push or a merge. Never
  merge into `main` or push it by hand, and never merge `main` into `dev`; the workflow makes the
  release commits. This replaced the earlier flow (every `main` push a release, `./scripts/release`)
  in Oct 2026.
- **Previews go to the Marketplace's pre-release track too** (decided with the user Oct 26, 2026;
  the Oct 22 plan had kept them on GitHub only, a preference, not a blocker). The minutes-based
  build number grows across previews and releases, so never introduce the odd/even minor
  convention, and never publish a version lower than one already on the Marketplace.
- **Whenever you are blocked or waiting on the user**, notify them: they are often not watching.
- A 401 in the Release workflow's Marketplace step means the `VSCE_PAT` secret (an Azure DevOps
  PAT, expires around July 2027) needs a new token.
- CI timing baseline (Oct 2026, before the Release workflow): a full gate took ~10.5 min.
  Per-browser e2e time varies ±30 s run to run (~3.4 s per test), so compare a range of runs, not two.

## Changes list

When a feature or larger change is done, add it to `docs/notes/CHANGES-2026.md` in the same work:
under its area section with the month or date, and in that month's line of "The year in short"
if it is headline-worthy. Skip bug fixes, package bumps and e2e flake fixes unless they change how
the project works. In 2027 start `CHANGES-2027.md` in the same format (ask the user first).

## Shell and build traps

- `pkill -f` / `pgrep -f` match their own Bash command line: a pattern that also appears elsewhere
  in the same command (say a later `dotnet run --project ./src/Dependinator.Web`) kills the
  tool's shell (exit 144) and silently skips the rest. Stop processes by pid, found by port
  (`lsof -ti tcp:5000 -sTCP:LISTEN`), in a call of their own, then build or start in the next call.
  `func start` and the `dotnet watch` host can ignore SIGTERM: `kill -9` them by pid, including
  the `Api.dll` func workers. `./scripts/watch` is a chain (script → `dotnet watch` → watch host
  → `dotnet run` → `Dependinator.Web`); a surviving watch host relaunches the app on the next
  edit, so kill every level.
- `dotnet csharpier format .` also rewrites XML files (`Directory.Packages.props` is not in its
  style): format single files, or revert the collateral XML diff.
- An incremental build can leave stale binaries (CSharpier.MsBuild touches sources; a running
  `dotnet watch` hot-reloads in process only). When a failure contradicts the source, check the
  dll's timestamp, delete the project's `obj/` and `bin/`, and rebuild.
- `./scripts/watch` restarted the app on every page load when a project's generated
  `obj/.../AssemblyInfo.cs` changed during a parse: the Roslyn parser's design-time build of this
  solution rewrote Core's `BuildTime` metadata, and dotnet watch restarts on any Compile item change.
  Core now skips AssemblyInfo in design-time builds; keep build-time-varying values out of them.
- `./scripts/watch` can die on hot reload (Roslyn `EmitHelpers.EmitDifference` exception) and
  leave Azurite/func holding ports 10000/7071. Check :5000 after edits.

## Decided or parked: don't re-propose unprompted

- UX review: parse diff and pinned containers are parked (`docs/notes/UX-REVIEW-SUMMARY.md`).
- Splitting `ModelService` / `AppCloudSyncService` was deferred as not worth the risk. If
  AppCloudSyncService is reworked anyway, extracting its refresh scheduler (with scheduler tests)
  is the sanctioned split.
- Pass-through namespaces are view-level (`Node.IsPassThrough`). Renaming nodes at parse time was
  rejected: it churns node identity and breaks references and search.
- AI help: a `claude -p` route on the user's subscription was postponed (the policy for a
  published extension is unclear), then built on Oct 26 at Michael's request as an experimental
  opt-in. First hidden (unlisted VS Code setting, env var), then moved the same day to Settings ›
  Use Claude Code (Experimental) with a warning, since Michael is the only user for now; he plans
  to hide it again later. Keep it out of the README and the tour.
- Line anchors stay centered edge midpoints; spreading them was tried and rejected.

## Ideas waiting for a session

- **Problems indicator, next:** phases 1-3 are done (Oct 21): `ProblemService` +
  `App/ProblemIndicator.razor` show open cycles and broken rules as warnings, and compile errors as
  errors (`ParseProblem` on `Item`, one per C# project or Bicep file, saved in
  `ModelDto.ParseProblems`). Decided with the user: errors only as a brief summary (the editor lists
  each error), no compile warnings for now (many users never fix them, so the indicator would always
  be on), rule violations counted per broken rule. Phase 3: `GetNodeProblems` counts each problem at
  its node and every ancestor, `NodeSvg.ProblemBadgeSvg` draws the badge (View menu toggle); badges
  are baked into tiles, so `SvgService` clears the tiles when that dictionary's instance changes.
  Still open: a namespace commented in several files and links to missing targets (delegate
  `Invoke`) as problems, unresolved rules (info, list only).
- **Hidden nodes, next:** phase 1 (Oct 23) made dim/hide per node (`NodeVisibility`, the outermost
  dimmed or hidden ancestor decides for its whole subtree, View › Reveal Hidden Nodes is a
  temporary peek). Phase 2 (Oct 23): `HiddenNodesPanel` lists the user-set nodes
  (`HiddenNodeService`, cached on `Model.VisibilityVersion`), opened from `HiddenNodesIndicator` in
  the app bar; going to a hidden node turns Reveal on. Phase 3 (Oct 23): `NodeSvg.HiddenBadgeSvg`,
  a grey count on the top-left of the direct parent of user-hidden nodes only (decided with the
  user: not every ancestor, for now), View toggle, node menu "List Hidden Children". The badge is
  not clickable (pointer events pass through, like the problem badge). Still open: hide rules (by
  pattern), once these have been lived with.
- **Ask your model (roadmap idea 5), next:** phase 1 (Oct 29) is the Ask panel (`AskPanel.razor`,
  `AskModel.cs`) on the Claude Code route with the MCP model tools; node names in backticks become
  links (short name in the text). Phase 2 (Oct 29): the API key route, `ModelToolsAgent`'s own
  tool-use loop (Sonnet, 15 turns) over the app's MCP server as a client (`McpAgentTools`), live
  lookup lines from `IModelQueries.Asked`, the cost per answer. Phase 3: follow-up questions, a `read_source` tool,
  a small eval set of questions. No paste route (decided with the user: multi-turn tools).
- **AI bulk review** of all or selected open cycles, each answer still signed off by the user.
  Settle first: a cost and time estimate (cents and up to a minute per cycle, ~2 at a time), the
  sign-off UX (verdict badges, a "Next" button), the paste route, cancellation.
- **Passkeys** (branch `mr/passkey`, unmerged, still on the old `Try()` pattern): a Clerk
  Dashboard toggle plus a "Manage Account" menu item; production needs a paid Clerk plan;
  domain-bound, so the item is hidden in the VS Code host.
- **Google/Apple sign-in:** a dashboard toggle on the Clerk development instance
  (`renewed-hen-98`), which production still uses. A Clerk production instance needs its own
  OAuth clients, DNS on dependinator.com, and the VS Code loopback sign-in page hosted on
  dependinator.com. Cloud storage is keyed by a hash of the email claim, so models survive a
  sign-in method switch.
