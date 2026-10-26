# Releasing Dependinator

A preview is built from `dev` and a release from `main`, and both are published only when someone
asks for one, with the **Release** workflow (`.github/workflows/release.yml`). Pushing to a branch,
or merging into `dev` or `main`, only runs the tests (`build.yml`); a pull request also gets its
own test site (`azure-static-web-apps-*.yml`). Anyone with write access can release, from a
browser if need be:

1. **Write the notes.** Edit the `## [Unreleased]` section at the top of `CHANGELOG.md` on `dev`, in
   an editor or GitHub's web editor, and commit it to `dev`. The style is below.
2. **Run the workflow.** On GitHub: Actions, **Release**, **Run workflow**, with **Use workflow
   from** set to `dev`, and pick `preview` or `release`. Or from a terminal:
   `gh workflow run release.yml --ref dev -f kind=preview` (or `kind=release`).

With Claude, ask it to "make a preview" or "make a release": it writes the notes from what changed
since the latest release, commits them and runs the workflow (the `release` skill,
`.claude/skills/release/SKILL.md`). A release it shows you first and waits for your go. Ask it
"what's unreleased?" for the notes alone, as they would be written now and what is new since the
latest preview, with nothing committed or published.

## What the workflow does

Every run tests and builds what it publishes: the unit tests, the e2e tests in Chromium, Firefox
and WebKit with the cloud-sync tests, the web app with its cloud sync API, and the VS Code
extension's six packages (Linux, Windows and macOS, each x64 and arm64). Each build checks that it
carries the version the run computed. Nothing is pushed, deployed or published before all of it
has passed.

**A preview** builds `dev` as it is and makes no commit. It deploys the web app to the `dev`
environment of Azure Static Web Apps (the pre-release site), publishes the extension to the VS Code
Marketplace's pre-release track, and publishes a GitHub pre-release tagged `v<version>` with the
extension's packages, its notes the `Unreleased` section so far. A preview with no notes yet is
allowed, and says so in a warning. The Releases page keeps the latest five previews; the
Marketplace keeps every version.

The Marketplace's pre-release track is opt-in: a user gets the previews after **Switch to
Pre-Release Version** on the extension's page in VS Code, and everyone else only the releases. A
preview's packages are built with `vsce package --pre-release`, which marks them as pre-release,
the GitHub pre-release's `.vsix` files too. VS Code updates to the highest version of the track a
user is on, so a pre-release user gets each release as well (it is newer than the previews before
it) and then the next preview. That is why the versions need no odd/even minor, the Marketplace's
usual convention for a pre-release built apart from the release: ours grow across both.

**A release**:

1. Refuses to run when `Unreleased` has no `- ` item, or when the version on `dev` is not above the
   latest release in major.minor.
2. On `dev`: renames `## [Unreleased]` to `## [v<version>] - <date>`, starts a new empty
   `## [Unreleased]` above it, and commits `Release v<version>`.
3. Merges `dev` into `main` with `--no-ff`, the notes as the merge message, and tests and builds
   that merge.
4. On `dev` again: raises `MinorVersion` in `Directory.Build.props` (and the extension's
   `package.json`) by one, `Start v<major>.<minor>`, so the previews that follow carry the number
   of the next release.
5. Only when everything has passed: pushes `dev` and `main` together, deploys the web app to
   production (dependinator.com), publishes the extension to the VS Code Marketplace, and
   publishes the GitHub release on the merge. If either branch moved meanwhile, the push fails and
   nothing is released; run it again.

So `dev` never needs `main` merged back, and `main` moves only by the workflow.

The push needs the `RELEASE_TOKEN` repository secret: a fine-grained personal access token for
this repository with **Contents** and **Workflows** read and write. GitHub refuses a push by the
workflow's own token that changes a file under `.github/workflows/`, which a release does whenever
`dev` changed a workflow since the last one; without the secret such a release fails at the push,
before anything is published. The token's push runs the Build workflow (tests only) on `dev` and
`main`. A branch protection rule on `dev` or `main` that requires pull requests needs a bypass for
the token's owner.

**A dry run:** leave **Publish** unticked. It tests and builds everything, prints the notes, the
commits it would push and the packages, and changes nothing.

**A run that failed after its push**, in the deploy, the Marketplace or the GitHub release, has
moved `dev` and `main` without finishing the release, and running the workflow again is refused
(the notes are used up). Use **Re-run failed jobs** on that run first: the publish job pushes the
same commits again (which does nothing), deploys again, skips a Marketplace version it has, and
creates the GitHub release if it is missing. If that cannot work (the run's artifacts expired after
90 days, or a secret has to be fixed first, which a re-run then picks up), finish it by hand from
the run's log, which prints the version and the merge's sha: download the run's
`dependinator-*-vsix` artifacts and
`gh release create v<version> --target <main's merge sha> --title v<version> --notes-file <notes> *.vsix`,
`npx @vscode/vsce publish --packagePath <file>` for each package, or ask Claude to. A 401 from the
Marketplace means the `VSCE_PAT` secret (an Azure DevOps PAT) needs a new token.

## Versions

A version is `major.minor.build`, e.g. `0.84.423503`: `MajorVersion` and `MinorVersion` from
`Directory.Build.props`, then the minutes since 2026-01-01 UTC of the build time. It is the same
everywhere: the tag, the GitHub release, the Marketplace and the app's **About** dialog, which also
shows the build time and the commit. The workflow sets the build number and time
(`BuildNumber`, `BuildTime`) for every build it makes; a local build is `major.minor.0`. Every
release is newer than the previews before it, the previews after it are newer than the release, and
the Marketplace takes only three-part versions, unique across releases and pre-releases.
`dev` always holds the major.minor of the next release; a new major is set by hand on `dev`, e.g.
`MajorVersion` 1 and `MinorVersion` 0, with the extension's `package.json` at `1.0.0`.

## How the notes are written

The notes are for the people who use Dependinator, not for its developers:

- Group the items under `### New`, `### Improved`, `### Changed` and `### Fixed`, in that order,
  leaving out the groups with nothing in them.
- One item per change a user notices, as one line starting with a `- `.
- Describe how Dependinator behaves now, in the present tense, never as an instruction to the
  reader: the group already says what kind of change it is. Under New, say what the app has or does
  ("Zones: labelled background areas for grouping nodes"); under Improved and Changed, what is
  different, often with "now" or "no longer" ("Hiding nodes can now be undone"); under Fixed, how it
  works now ("A node framed by **Show node** stays small enough to be drawn as an icon"). Not
  "Show ...", "Add ...", which read as commands.
- Leave out what users do not see: refactoring, tests, build and CI changes, and the README.
- Fold a later change to a feature into its item, rather than listing both.
- Write menu items and commands in bold, as the app names them (**View › Lines**,
  **Reset Children Layout**), and keys as `` `Ctrl-Z` ``.
- A mistake in the notes of a version already released can be fixed in `CHANGELOG.md`; its GitHub
  release keeps the notes it was published with, unless they are edited there too. The extension's
  Marketplace page shows `CHANGELOG.md` as it was packaged.
