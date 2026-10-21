# Workflow rules and decisions

How this project is worked on, beyond CLAUDE.md. Always loaded, in local and cloud sessions alike.

## Merges and releases

- **Review before merging into `dev`.** Have the commits to be merged (`dev..<branch>`) reviewed
  in a fresh context first: the `code-review` skill on `<branch>` against `dev`, or a new session.
  Commits that fix review findings count as unreviewed until someone reviews them. Report the
  findings and get the user's go-ahead before merging. One such review found 15 issues, among
  them a cache bug that would have shipped.
- **Merge messages are release notes:** one summary line, then "- " bullets of what changed for
  the user (not commit subjects or internals).
  - dev: `git merge --no-ff <branch> -F <file>`; write the message to a file first
    (`git merge -F -` does not read stdin).
  - main: `./scripts/release -m "…"` once per bullet, gathered from the dev merge messages since
    the last release. Without `-m` it lists every commit subject since the last tag; it ignores
    merge bodies. Preview with `-n`.
- **The release flow is settled:** every main push is an automatic release, Dependabot targets dev
  (grouped), `./scripts/release` merges dev into main. Declined, don't re-propose: a
  version-driven release, a manual-dispatch-only release, gating the production deploy on a
  release, making dev the default branch.
- A 401 in the extension's publish step means the `VSCE_PAT` secret (an Azure DevOps PAT,
  expires around July 2027) needs a new token. `./scripts/build-ext` bumps the patch version
  locally, which can diverge from CI's bump.
- CI timing baseline (Oct 2026): a dev push's CI/CD takes ~10.5 min, a release ~15. Per-browser
  e2e time varies ±30 s run to run (~3.4 s per test), so compare a range of runs, not two.

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
- `./scripts/watch` can die on hot reload (Roslyn `EmitHelpers.EmitDifference` exception) and
  leave Azurite/func holding ports 10000/7071. Check :5000 after edits.

## Decided or parked: don't re-propose unprompted

- UX review: parse diff and pinned containers are parked (`docs/notes/UX-REVIEW-SUMMARY.md`).
- Splitting `ModelService` / `AppCloudSyncService` was deferred as not worth the risk. If
  AppCloudSyncService is reworked anyway, extracting its refresh scheduler (with scheduler tests)
  is the sanctioned split.
- Pass-through namespaces are view-level (`Node.IsPassThrough`). Renaming nodes at parse time was
  rejected: it churns node identity and breaks references and search.
- AI help: a `claude -p` subprocess on the user's subscription was postponed (the policy for a
  published extension is unclear); the copy-prompt and copy-fix-prompt buttons are the accepted
  in-between.
- Line anchors stay centered edge midpoints; spreading them was tried and rejected.

## Ideas waiting for a session

- **Problems indicator, phase 2:** phase 1 (Oct 21, `ProblemService` + `App/ProblemIndicator.razor`)
  shows open/to-fix cycles and broken rules as warnings. Agreed with the user for phase 2: parse
  problems as data, an optional `Problem` on `Item` (like `LineDescription`) over the `[Rpc]` parse
  API, saved with the model (`ModelDto`); compile errors only as a brief per-project summary (the
  user checks VS Code for each error), no compile warnings for now (many users never fix them,
  so the indicator would always be on). Also: a namespace commented in several files, links to
  missing targets (delegate `Invoke`), unresolved rules (info, list only). Phase 3: badges on
  nodes, aggregated up the hierarchy. Rule violations count per broken rule, not per type pair.
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
