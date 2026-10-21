---
paths:
  - "tests/Dependinator.E2E.Tests/**"
  - "scripts/e2e"
---

# e2e tests: rules learned from failures

Read together with `tests/Dependinator.E2E.Tests/README.md` (parallelism cap, verify-and-retry
page objects). Each rule below comes from a real CI or local failure.

## Before re-running or debugging

- Several unrelated tests failing at once, with `accounts.clerk.accounts.dev` and a Cloudflare
  "security verification" page in the trace: environmental, rerun.
- A different test failing each run with no code change: suspect parallelism or machine load
  (`maxParallelThreads` is 2 because the default gave ~50% failing runs). Re-measure the baseline
  in the same session before bisecting app commits; before raising the cap, run the suite a
  dozen times and count failures. Not the cause: the app's memory growth (~1 GB RSS per suite
  run, mostly large tile-SVG strings).
- A timeout at ~15 s is a real render stall (`E2ETestBase` sets the Expect timeout to 15 s because
  the 5 s default flaked on slow runners): read the trace (`E2E_TRACE=1`, `./scripts/trace`; CI
  uploads `playwright-traces-<browser>`) instead of re-running.
- `LineDimTests` failed once in a full run and passed 3/3 alone: rerun a suspect class alone first
  (`E2E=1 BROWSER=chromium E2E_BASE_URL=http://localhost:5000 dotnet test
  tests/Dependinator.E2E.Tests --no-build --filter "FullyQualifiedName~LineDimTests"` against an
  app started with `DEPENDINATOR_E2E=1`); investigate if it fails alone.
- A very slow but green firefox/webkit run is a Blazor circuit reconnecting (unfixed; the 5 min
  `JSInteropDefaultCallTimeout` in the Web `Program.cs` is a suspect).
- Fixed flakes whose return means the fix fell short. Read the trace; don't just re-run:
  - menu-open timeouts despite the `AppPage` retries;
  - truncated note text (the Blazor echo window exceeds `FillReliablyAsync`'s 250 ms);
  - a search that finds nothing (one of the parse-wait layers regressed);
  - Settings submenu checks.

## Driving the app

- Verify every gesture through the `AppPage` helpers; ask what UI change proves an action
  landed, and wait for it.
- Never act on state you have not seen. Blazor Server applies state on the server, so an Escape or
  drag sent right after a click races mode arming or post-navigation selection: use
  `App.CancelModeAsync()`, `App.DeselectAsync()`, and `Expect(App.ModeBanner).ToBeVisibleAsync()`
  before dragging an armed area.
- The app ignores canvas shortcuts while a dialog or menu popover is open, including the fade-out
  after a menu click. `AppPage.OpenSearchViaHotkeyAsync` waits for `KeyboardOwners` to be empty
  and retries; any shortcut sent after a menu or dialog needs the same wait.
- A keystroke to a MudBlazor dialog that is still animating in is lost: `SearchDialog.CloseAsync`
  presses Escape until the field is hidden. Put verify-and-retry in page objects, not sleeps in
  tests.
- The first Escape after a submenu opens is always lost (all browsers), so `CloseMenuAsync` costs
  5 s while a submenu is open. Never hover a nested submenu item with a bare `HoverAsync`: use the
  three-argument `OpenSubMenuItemAsync(parent, child, item)`. Check a submenu item's state with
  `ExpectSubMenuItemCheckedAsync` (reopens per attempt); never re-click a toggle to retry it.
- Fill inputs with `AppPage.FillReliablyAsync` (a Blazor echo can overwrite a fill). Open search
  only once the model has rendered (`WaitForModelRenderedAsync`), and wait for
  `search.SelectedItem` before pressing Enter (Enter on an empty result list does nothing).
- Press keys on the element (`search.Field.PressAsync("Enter")`), not through `Page.Keyboard`: a
  global keystroke is lost when the field momentarily loses focus.
- A node's label class depends on how it renders (`text.iconName`, `text.nodeName`,
  `text.memberName`, zones `text.zoneName`): label locators match all of them
  (`NodeLabelSelector`). Container-only assertions call `WaitForContainerNodeAsync` first, since
  the zoom decides whether a node draws as container or icon.
- Clicks resolve through `event.target.id`: a click inside a container selects the child under it,
  and a node's icon or name carries no id. `SelectContainerNodeAsync` tries points in the header
  strip.
- At the initial view only `Demo.sln` is selectable, so `SelectContainerNodeAsync("Demo.UI")`
  fails. Navigate in first: `App.NavigateToNodeAsync(AppPage.InsideMain)`, then
  `WaitForContainerNodeAsync("Main")`, then `SelectContainerNodeAsync("Demo.UI.Main")`.
- Navigate by exact full name (`App.NavigateToNodeAsync`, `SearchDialog.ResultByFullName`), never
  by a short fuzzy query: its ranking lands unpredictably (even "Demo.UI.Main" lists the member
  `OnInitialized()` above the class, a known unfixed wart).
- Edit mode is off by default: tests that edit call `App.EnableEditModeAsync()`. Nested Bicep
  nodes are addressed by long name (`App.Node("rgPlatform.networking")`); the test host reopens
  the C# demo on reload, so Bicep persistence tests reopen it from the Models menu.

## Browsers

Feature-branch pushes run chromium only; the dev push runs all three browsers plus sync, so
browser-specific bugs surface there. Run touched tests with `-b firefox` and `-b webkit` before
trusting a chromium-only run for a release.

- Clipboard: `GrantPermissionsAsync(["clipboard-read", "clipboard-write"])` throws on firefox and
  webkit. Guard on `BROWSER`, or stub `navigator.clipboard` with `Page.AddInitScriptAsync`.
- WebKit keeps a stale `:hover` after a re-render: click hover-revealed buttons with
  `AppPage.ClickRowHoverButtonAsync(row, testId)`.
- WebKit misreports the box of a standalone canvas `<text>` (offset by its own x/y): click through
  the `g.hoverable` group box (`App.Node(label)`), never a text box.

## State shared between parallel tests

One `Dependinator.Web` process serves both parallel test classes, so statics are shared by all
circuits, and a parallel test's page load resets a static toggle mid-test. Per-user settings live
in scoped services (`IViewOptions`, `IPointerEventService`); `DColors.IsDark` and
`NodeLayout.SetDensity` are still static. Theme tests assert per-circuit signals (html
`data-theme`) and restore Light. Close menus with `App.CloseMenuAsync()` before wheel gestures (an
open menu overlay swallows them).

## Demo-model facts the tests rely on

`src/Dependinator.Wasm/wwwroot/demo.model` (the solution parsed, "Dependinator" renamed "Demo"):
- `AppPage.InsideMain` = `Demo.UI.Main.OnInitialized()`, and Main's `DemoTheme` member
  (shift-click test).
- `Demo.UI.Main` reaches `Demo.Core.Shared.ModelPaths` in 3 hops (Main → Canvas → AppBar →
  ModelPaths) with no direct Core dependency; Main has lines to Externals/MudBlazor.
- At the InsideMain view: `Demo.UI.Main→Demo.UI.Shared (7)` (2.4 px while Main is selected) and
  `Demo.UI.Main→Demo.UI.Diagrams (1)` (stays 1 px).
- Cycles are picked by name, never `.First`: the `Result ↔ Result<T>` pair is the accept case in
  test mode, a "TaskExtensions" cycle the break case.

After `./scripts/gen-demo csharp` run the full e2e suite (a regeneration once broke nine tests);
regenerating the Bicep demo is safe.
