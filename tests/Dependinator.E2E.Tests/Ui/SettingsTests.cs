using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the Settings dialog (Menu › Settings …): "Include test projects" is a checkbox
// whose state is stored per model. The demo model is pre-parsed and embedded, so the triggered
// re-parse short-circuits and the node set does not change -- this asserts the checkbox state
// itself, which is what the dialog owns. "Invert scroll zoom" is an app-wide preference stored
// in the config that flips the mouse wheel zoom direction, the AI section picks the AI the app
// asks by default and turns on the experimental Claude Code route.
// The layout density is tested on its own, without parallel tests (LayoutDensityTests).
public class SettingsTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task IncludeTestProjects_ShouldToggleViaSettingsDialog()
    {
        await App.GotoMainPageAsync();

        // Test projects are excluded by default.
        await App.ExpectSettingAsync("settings-include-test-projects", isChecked: false);

        await ToggleIncludeTestProjectsAsync(expectOn: true);

        // And back off again.
        await ToggleIncludeTestProjectsAsync(expectOn: false);
    }

    // Settings › AI: the experimental Claude Code route is off by default and turned on only
    // through a warning that can be cancelled; the default provider is what the review dialogs
    // ask, and their menu asks another one once (answered by the app itself in test mode).
    [E2EFact]
    public async Task AiSettings_ShouldAskBeforeClaudeCode_AndSetTheProviderTheDialogsAsk()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.OpenSettingsAsync();
        ILocator useCode = Page.GetByTestId("ai-settings-use-claude-code");
        await Expect(useCode).ToHaveAttributeAsync("data-checked", "false");
        await Expect(Page.GetByTestId("ai-settings-no-default")).ToBeVisibleAsync();

        // Cancelling the warning leaves it off.
        await useCode.ClickAsync();
        ILocator confirm = Page.GetByTestId("use-claude-code-confirm");
        await Expect(confirm).ToBeVisibleAsync();
        await Page.GetByTestId("use-claude-code-cancel").ClickAsync();
        await Expect(confirm).ToHaveCountAsync(0);
        await Expect(useCode).ToHaveAttributeAsync("data-checked", "false");
        await App.CloseSettingsAsync();
        await Expect(useCode).ToHaveCountAsync(0);

        // On, and the default: the dialog's one button asks Claude Code.
        await App.SetAiSettingsAsync(useClaudeCode: true, defaultProvider: "code");
        await OpenCycleReviewAsync(isChoosing: false);
        ILocator ask = Page.GetByTestId("accept-cycle-ask");
        await Expect(ask).ToHaveAttributeAsync("data-provider", "code");
        await Expect(ask).ToHaveTextAsync("Ask Claude Code");
        await Expect(Page.GetByTestId("accept-cycle-route-claude")).ToHaveCountAsync(0);
        await ask.ClickAsync();
        await Expect(Page.GetByTestId("accept-cycle-suggestion")).ToContainTextAsync("Claude Code");

        // The menu asks Claude once; the default stays.
        await Page.GetByTestId("accept-cycle-ask-menu").ClickAsync();
        await Page.GetByTestId("accept-cycle-ask-with-claude").ClickAsync();
        ILocator answers = Page.GetByTestId("accept-cycle-answer");
        await Expect(answers).ToHaveCountAsync(2);
        await Expect(answers.Last).ToContainTextAsync("Claude ·");
        await Expect(ask).ToHaveAttributeAsync("data-provider", "code");

        // The menu's "AI settings …" opens the Settings dialog, at its AI section.
        await Page.GetByTestId("accept-cycle-ask-menu").ClickAsync();
        await Page.GetByTestId("accept-cycle-ask-settings").ClickAsync();
        await Expect(Page.GetByTestId("settings-ai-section")).ToBeInViewportAsync();
        await App.CloseSettingsAsync();
        await CloseCycleReviewAsync();

        // Off again (no confirmation): the default is gone with it, so the dialog shows the
        // providers to choose from, without Claude Code.
        await App.OpenSettingsAsync();
        await useCode.ClickAsync();
        await Expect(useCode).ToHaveAttributeAsync("data-checked", "false");
        await Expect(Page.GetByTestId("ai-settings-no-default")).ToBeVisibleAsync();
        await App.CloseSettingsAsync();
        await Expect(useCode).ToHaveCountAsync(0);
        await OpenCycleReviewAsync(isChoosing: true);
        await Expect(Page.GetByTestId("accept-cycle-route-claude")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("accept-cycle-route-code")).ToHaveCountAsync(0);
        await CloseCycleReviewAsync();
    }

    // Settings › AI: each of Claude's routes has a model picker (the API key's models, in test
    // mode a fixed list; Claude Code's aliases or a typed name), and the picks are kept.
    [E2EFact]
    public async Task AiSettings_ShouldKeepTheModelsPickedForClaudeAndClaudeCode()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.OpenSettingsAsync();
        ILocator claudeModel = Page.GetByTestId("ai-settings-claude-model-row");
        ILocator codeModel = Page.GetByTestId("ai-settings-claude-code-model-row");
        await Expect(claudeModel).ToHaveAttributeAsync("data-value", "");
        await Expect(claudeModel).ToContainTextAsync("Automatic");
        // Claude Code's model shows only once the route is on.
        await Expect(codeModel).ToHaveCountAsync(0);

        await PickModelAsync(claudeModel, "Claude Sonnet 5.5");
        await Expect(claudeModel).ToHaveAttributeAsync("data-value", "claude-sonnet-5-5");
        await App.CloseSettingsAsync();

        await App.SetAiSettingsAsync(useClaudeCode: true);
        await App.OpenSettingsAsync();
        await Expect(claudeModel).ToHaveAttributeAsync("data-value", "claude-sonnet-5-5");
        await Expect(codeModel).ToHaveAttributeAsync("data-value", "");
        await PickModelAsync(codeModel, "Opus (latest)");
        await Expect(codeModel).ToHaveAttributeAsync("data-value", "opus");

        // Another model is typed by name.
        ILocator name = Page.GetByTestId("ai-settings-claude-code-model-name");
        await Expect(name).ToHaveCountAsync(0);
        await PickModelAsync(codeModel, "Another model");
        await App.FillReliablyAsync(name, "claude-opus-5-5");
        await name.PressAsync("Tab");
        await Expect(codeModel).ToHaveAttributeAsync("data-value", "claude-opus-5-5");
        await App.CloseSettingsAsync();

        await App.OpenSettingsAsync();
        await Expect(codeModel).ToHaveAttributeAsync("data-value", "claude-opus-5-5");
        await Expect(name).ToHaveValueAsync("claude-opus-5-5");

        // Back to the defaults, for the tests that share the app's settings.
        await PickModelAsync(codeModel, "Automatic");
        await Expect(codeModel).ToHaveAttributeAsync("data-value", "");
        await PickModelAsync(claudeModel, "Automatic");
        await Expect(claudeModel).ToHaveAttributeAsync("data-value", "");
        await Page.GetByTestId("ai-settings-use-claude-code").ClickAsync();
        await Expect(codeModel).ToHaveCountAsync(0);
        await App.CloseSettingsAsync();
    }

    // Opens a model select and clicks the item with the text.
    async Task PickModelAsync(ILocator modelRow, string itemText)
    {
        await modelRow.Locator(".mud-select").First.ClickAsync();
        await Page.Locator(".mud-popover-open .mud-list-item").Filter(new() { HasTextString = itemText }).ClickAsync();
    }

    // Opens the review dialog of a cycle, its AI section open: without a default the providers wait
    // behind a row that opens them, with one the default's button shows at once.
    async Task OpenCycleReviewAsync(bool isChoosing)
    {
        ILocator pair = Page.GetByTestId("cycles-item").Filter(new() { HasTextString = "Result<T>" });
        // The panel stays open after the first time.
        if (!await pair.IsVisibleAsync())
            await (await App.OpenSubMenuItemAsync("menu-view", "menu-cycles")).ClickAsync();
        await pair.GetByTestId("cycles-review").ClickAsync();
        ILocator expand = Page.GetByTestId("accept-cycle-expand");
        if (isChoosing)
            await expand.ClickAsync();
        else
            await Expect(expand).ToHaveCountAsync(0);
    }

    async Task CloseCycleReviewAsync()
    {
        await Page.GetByTestId("accept-cycle-cancel").ClickAsync();
        await Expect(Page.GetByTestId("accept-cycle-cancel")).ToHaveCountAsync(0);
    }

    // Only wheel-down (a positive delta) is used to probe the zoom direction: headless
    // Chromium does not reliably deliver negative-delta wheel events to the page.
    [E2EFact]
    public async Task InvertScrollZoom_ShouldFlipWheelZoomDirection_AndPersistAcrossReload()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        // Off by default: wheel down zooms out, so the root node shrinks on screen.
        await ExpectInvertScrollZoomAsync(isOn: false);
        await ExpectWheelDownToZoomAsync(zoomIn: false);

        // On: the same wheel down now zooms in, so the node grows.
        await ToggleInvertScrollZoomAsync(expectOn: true);
        await ExpectWheelDownToZoomAsync(zoomIn: true);

        // The preference is stored in the config, so it is still on after a reload.
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await ExpectInvertScrollZoomAsync(isOn: true);
        await ExpectWheelDownToZoomAsync(zoomIn: true);

        // And back off again.
        await ToggleInvertScrollZoomAsync(expectOn: false);
        await ExpectWheelDownToZoomAsync(zoomIn: false);
    }

    // Assert the checkbox via the dialog (which is closed again, so the canvas is free for
    // wheel gestures: an open dialog's overlay would swallow them).
    Task ExpectInvertScrollZoomAsync(bool isOn) => App.ExpectSettingAsync("settings-invert-scroll-zoom", isOn);

    // Click the checkbox and verify it really flipped (AppPage.ToggleSettingAsync).
    Task ToggleInvertScrollZoomAsync(bool expectOn) => App.ToggleSettingAsync("settings-invert-scroll-zoom", expectOn);

    // Scroll the wheel down over empty canvas and assert which way the root node changes size.
    // A wheel landing while the canvas re-renders is swallowed silently, so the gesture is
    // repeated until the node size actually changes.
    async Task ExpectWheelDownToZoomAsync(bool zoomIn)
    {
        LocatorBoundingBoxResult canvas =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");
        // The demo nodes sit at the canvas center; a quarter in from the top-left is empty
        // canvas, so no node toolbar pops up under the pointer and takes the wheel event.
        await Page.Mouse.MoveAsync(canvas.X + canvas.Width / 4, canvas.Y + canvas.Height / 4);

        // Measure only once the view has settled, so a size change can only come from the wheel.
        float before = await SettledRootNodeWidthAsync();
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            await Page.Mouse.WheelAsync(0, 100);

            float after = before;
            for (int poll = 0; poll < 20 && Math.Abs(after - before) < 1; poll++)
            {
                await Page.WaitForTimeoutAsync(100);
                after = await RootNodeWidthAsync();
            }

            if (Math.Abs(after - before) >= 1)
            {
                Assert.True(
                    zoomIn ? after > before : after < before,
                    $"Expected wheel down to zoom {(zoomIn ? "in" : "out")}, but root node width went {before} -> {after}."
                );
                return;
            }
        }

        Assert.Fail("Wheel down never changed the zoom.");
    }

    // The root node's on-screen width once it has stopped changing (e.g. after the initial
    // fit-to-view or a pending zoom re-render), polled up to a few seconds.
    async Task<float> SettledRootNodeWidthAsync()
    {
        float width = await RootNodeWidthAsync();
        for (int poll = 0, stable = 0; poll < 50 && stable < 3; poll++)
        {
            await Page.WaitForTimeoutAsync(100);
            float next = await RootNodeWidthAsync();
            stable = Math.Abs(next - width) < 0.5f ? stable + 1 : 0;
            width = next;
        }
        return width;
    }

    async Task<float> RootNodeWidthAsync()
    {
        LocatorBoundingBoxResult box =
            await App.Node("Demo.sln").First.BoundingBoxAsync()
            ?? throw new InvalidOperationException("Root node is not rendered.");
        return box.Width;
    }

    // Click the checkbox and verify the state flipped. The click re-parses the model, and the
    // re-parse's completion re-renders the app; so a refresh in progress (the discreet spinner)
    // is let finish before the state is read again in a freshly opened dialog. That wait can
    // pass before the spinner shows (the embedded demo re-parses in an instant), so the
    // reopened read is what makes the check reliable. The click is not retried: clicking a
    // checkbox twice would flip it back.
    async Task ToggleIncludeTestProjectsAsync(bool expectOn)
    {
        await App.OpenSettingsAsync();
        ILocator setting = Page.GetByTestId("settings-include-test-projects");
        await setting.ClickAsync();
        await Expect(setting).ToHaveAttributeAsync("data-checked", expectOn ? "true" : "false");
        await App.CloseSettingsAsync();
        await Expect(Page.Locator(".app-progress-discreet")).ToHaveCountAsync(0);
        await App.ExpectSettingAsync("settings-include-test-projects", expectOn);
    }
}
