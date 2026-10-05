using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the breadcrumb (Breadcrumb.razor): the open model, then the chain of containers down
// to the selected node or, with nothing selected, to the innermost open container under the
// viewport center. Crumbs navigate; the model crumb fits the diagram to the screen.
public class BreadcrumbTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Breadcrumb_ShouldShowModelAndContainerChain_AndNavigate()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        // At the overview only the model crumb shows: the center is not inside an open container.
        ILocator model = Page.GetByTestId("breadcrumb-model");
        ILocator items = Page.GetByTestId("breadcrumb-item");
        await Expect(model).ToContainTextAsync("Demo.sln");
        await Expect(items).ToHaveCountAsync(0);

        // Jump to a member of Demo.UI.Main: it becomes the selected node, so the chain ends at it.
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await Expect(items.Last).ToContainTextAsync("OnInitialized");

        // Deselecting keeps the location: the view is now centered inside the open Main
        // container, so the "you are here" chain ends there.
        await Page.Keyboard.PressAsync("Escape");
        await Expect(App.NodeToolbarMenu).ToBeHiddenAsync();
        await Expect(items.Last).ToHaveTextAsync("Main");

        // Alt+Up zooms out one level: the innermost open container (Main) is framed inside its
        // parent, where it draws as an icon, and becomes the selected (last) crumb.
        await Page.Keyboard.PressAsync("Alt+ArrowUp");
        await Expect(items.Last).ToHaveTextAsync("Main");
        await Expect(Page.GetByTestId("node-toolbar")).ToHaveAttributeAsync("data-container", "false");

        // The model crumb fits the whole diagram: with nothing selected the chain is empty again
        // at the overview (a selected node would keep its chain, wherever the view is).
        await Page.Keyboard.PressAsync("Escape");
        await Expect(App.NodeToolbarMenu).ToBeHiddenAsync();
        await model.ClickAsync();
        await Expect(items).ToHaveCountAsync(0);
        await Expect(App.NodeLabel("Demo.sln")).ToBeVisibleAsync();
    }
}
