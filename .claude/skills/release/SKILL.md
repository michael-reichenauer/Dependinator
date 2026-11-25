---
name: release
description: Make a preview or a release of Dependinator, or answer "what's unreleased?". Use when the user asks for a preview, a pre-release, a release, a production or real release, or for what has not been released yet.
---

# Releasing Dependinator

`docs/RELEASING.md` is the reference: what the Release workflow does, how versions work, how the
notes are written and what to do with a run that failed after its push. Read it before the first
release of a session. This skill is the order of work.

A preview or a release is made only when the user asks for one, and then it starts right away:
asking for a "release", a "production release" or a "real release" is the go, as for a preview.
Ask only when it is unclear which of the two is meant.

## 1. Find what is new

The newest `v*` tags are the latest release (its tag is on `main`) and the previews since (on
`dev`). What a preview or release adds is what was merged into `dev` after the latest tag: read
those merges and their pull requests, not only their titles, since the notes describe what a user
notices.

## 2. Write the notes

The notes are the `## [Unreleased]` section of `CHANGELOG.md` on `dev`, in the style of "How the
notes are written" in `docs/RELEASING.md`. `scripts/changelog unreleased` prints what is there.

- Add an item for each change users notice that has none yet, and fold a later change to a
  feature into its item. Changes users do not see get no item.
- Commit the notes on `dev` and push it (`Write the notes for the v<major>.<minor> preview`, or
  `release`). This commit is the one exception to the rule that `dev` moves only through pull
  requests: the workflow builds `dev` as it is, so the notes must be there before the run.
- When the section already says everything, make no commit.

A preview with no notes yet is allowed. A release refuses to run while `Unreleased` has no item.

## 3. Run the workflow

Start the **Release** workflow (`release.yml`) from `dev`, also for a release, with `kind` set to
`preview` or `release`:

```bash
gh workflow run release.yml --ref dev -f kind=preview
```

When `gh` is not available, dispatch the same workflow with the GitHub tools: ref `dev`, the
same `kind`. `publish=false` is a dry run that tests, builds and prints what it would release.

Reply as soon as the run has started, with the kind, the notes as they will be published and a
link to the run. Then follow the run, and report the version and where it was published, or what
failed.

## 4. When a run fails

- Before its push (a test or a build): nothing was published. Read the failed job's log first.
  A flaky e2e test is re-run with **Re-run failed jobs**; a real failure is fixed through a pull
  request, then the workflow is run again.
- While publishing (the deploy, the Marketplace or the GitHub release): use **Re-run failed
  jobs** on that run, as "A run that failed after its push" in `docs/RELEASING.md` describes.
  A release has pushed `dev` and `main` by then, and a new release run is refused because the
  notes are used up. A new preview run would publish a second version beside the half-published
  one.

## "What's unreleased?"

Answer from the same sources with nothing committed or published, in two sections: "In a
preview, not yet released" and "On dev, not yet in any preview". List the pull requests in each,
with the note each one gets or would get.
