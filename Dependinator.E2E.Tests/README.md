# Dependinator.E2E.Tests — Playwright UI tests

Browser-based end-to-end tests using [Playwright for .NET](https://playwright.dev/dotnet/)
with xUnit, testing the app as a user sees it at `http://localhost:5000`.

## Running

```bash
./e2e                # chromium (default), auto-starts the app if needed
./e2e -b firefox     # specific browser: chromium | firefox | webkit
./e2e -a             # all three browsers (recommended before releases)
./e2e -s             # also start the cloud-sync stack so sync tests run
```

If `./watch` or `./watch-sync` is already running, `./e2e` reuses that app
instance (so hot-reloaded changes are tested); otherwise it starts and stops
`Dependinator.Web` itself.

### Deterministic model (test mode)

`./e2e` starts the app with `DEPENDINATOR_E2E=1`, which puts it in **test mode**
(`Build.IsTestMode`): on startup it loads the embedded **demo model** (`/Demo.sln`,
a pre-parsed snapshot served from `Dependinator.Roslyn`'s embedded `demo.model`)
instead of parsing the working solution. Tests therefore get a fast, deterministic
model — the root node is `Demo.sln`. If you instead reuse a plain `./watch` (not in
test mode), the app loads the real solution and model-specific assertions will
differ; let `./e2e` start the app for determinism.

### Cloud-sync tests

Sync features talk to the Azure Functions API on port 7071 (backed by Azurite).
Tests that need it are marked `[SyncFact]` instead of `[E2EFact]` and are
skipped unless that stack is up. `./e2e -s` brings it up the same way
`./watch-sync` does — reusing an already-running Azurite/Functions host, or
starting (and stopping) them itself. It requires `func` and `azurite` installed.
Note that signed-in sync flows additionally need a Clerk test user (not yet set
up), so `[SyncFact]` currently only covers behavior reachable without sign-in.

These tests are **skipped** during plain `dotnet test Dependinator.sln` — they
only run when the `E2E=1` environment variable is set, which `./e2e` does.
Equivalent manual invocation:

```bash
E2E=1 BROWSER=firefox dotnet test Dependinator.E2E.Tests/Dependinator.E2E.Tests.csproj
```

`E2E_BASE_URL` overrides the target app (e.g. `http://localhost:5000` for the
Wasm host started with `./run`).

## Playwright in 60 seconds

- **Locators** describe how to find an element and are evaluated lazily:
  `Page.GetByPlaceholder("Search nodes…")`, `Page.GetByRole(AriaRole.Button)`,
  `Page.Locator("#svgcanvas")`. Prefer user-visible attributes (role, text,
  placeholder) over CSS classes.
- **Auto-waiting**: actions (`ClickAsync`, `FillAsync`) and assertions
  (`Expect(...).ToBeVisibleAsync()`) automatically retry until the element is
  ready or a timeout (5 s for assertions, 30 s for actions) expires. Never use
  `Task.Delay` to "wait for the UI".
- **Web-first assertions**: always `await Expect(locator).ToBeVisibleAsync()` /
  `ToHaveTextAsync(...)` rather than asserting on snapshot values you read
  manually — the retry behavior is what makes tests stable.
- A fresh **browser context** (cookies/storage isolated, like an incognito
  window) is created per test by the `PageTest` base class; `Page` is ready to
  use in each test.

## Writing a new test

Add a class inheriting `E2ETestBase`, mark tests with `[E2EFact]`
(a `[Fact]` that is skipped unless `E2E=1`), name them `Thing_ShouldBehavior`:

```csharp
public class MyFeatureTests : E2ETestBase
{
    [E2EFact]
    public async Task MyFeature_ShouldDoSomething()
    {
        await Page.GotoAsync("/");
        await Page.Keyboard.PressAsync("Control+f");
        await Expect(Page.GetByPlaceholder("Search nodes…")).ToBeVisibleAsync();
    }
}
```

Useful app-specific selectors:

- **`data-testid` on menus and toolbar buttons** — prefer these over `mud-*`
  classes; use `Page.GetByTestId("...")`. Menu items only exist in the DOM while
  their menu is open, so click the activator first:
  ```csharp
  await Page.GetByTestId("appbar-menu").ClickAsync();   // open the main menu
  await Page.GetByTestId("menu-search").ClickAsync();   // then click an item
  ```
  Hooks: app menu activator `appbar-menu`; its items `menu-undo`, `menu-redo`,
  `menu-search`, `menu-fit-to-screen`, `menu-refresh`, `menu-show-hidden-nodes`,
  `menu-edit-mode`, `menu-reset-model`, `menu-local-paths`, `menu-cloud-models`,
  `menu-login`/`menu-logout`, `menu-about`; toolbar buttons `toolbar-cloud`,
  `toolbar-search`, `toolbar-undo`, `toolbar-redo`, `toolbar-refresh`,
  `toolbar-edit`. The node context toolbar uses `node-*` (e.g. `node-menu`,
  `node-references`, `node-show-source`) and the line toolbar `line-*`. Add a new
  `data-testid` to the component when you need to target something not listed.
- `#svgcanvas` — the diagram SVG. Nodes and lines already carry stable `id`s
  derived from their element id (e.g. `<rect id="{elementId}">`, see
  `PointerId.From*(...).ElementId`), so target them with `Page.Locator("#<id>")`.
  Other SVG contents are mostly invisible to role-based locators; assert on
  rendered text via `Page.Locator("#svgcanvas").GetByText(...)` or screenshots.
- `Ctrl+F` opens the search dialog (placeholder `Search nodes…`).
- MudBlazor components render with `mud-*` CSS classes (a fallback when no
  `data-testid` exists).

## Debugging failing tests

The devcontainer is headless — headed mode and Playwright's UI tools don't work
here. Instead:

- **Traces** (best): run with tracing, then open the trace viewer in a browser
  on the Mac at <https://trace.playwright.dev> (drag the zip in). Capture one
  ad hoc by setting `PWDEBUG=0` and adding to a test:
  `await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });`
  … `await Context.Tracing.StopAsync(new() { Path = "trace.zip" });`
- **Screenshots**: `await Page.ScreenshotAsync(new() { Path = "shot.png" });`
- **Console output**: Playwright errors include the action log (what it waited
  for and why it timed out) — usually enough to spot a bad selector.

## Version pinning

`Microsoft.Playwright.Xunit` (Directory.Packages.props) and `PLAYWRIGHT_VERSION`
in `.devcontainer/post-create.sh` must stay on the same version so the tests
find matching browser builds in `~/.cache/ms-playwright`. When bumping the NuGet
package, update the script and run it once (or rebuild the devcontainer).

Signed-in cloud-sync flows are not covered yet — they need a Clerk test-user
strategy first.
