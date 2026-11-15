using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the breadcrumb (Breadcrumb.razor): shown only while a node is selected (or a go to
// highlighted one), with the open model, then the chain of containers down to that node. Crumbs navigate; the
// model crumb fits the diagram to the screen.
public class BreadcrumbTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Breadcrumb_ShouldShowOnlyForASelectedNode_AndNavigate()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        // With nothing selected there is no breadcrumb.
        ILocator breadcrumb = Page.GetByTestId("breadcrumb");
        ILocator model = Page.GetByTestId("breadcrumb-model");
        ILocator items = Page.GetByTestId("breadcrumb-item");
        await Expect(breadcrumb).ToBeHiddenAsync();

        // Jump to a member of Demo.UI.Main: it becomes the selected node, so the breadcrumb
        // shows the model and the chain ending at it.
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await Expect(model).ToContainTextAsync("Demo.sln");
        await Expect(items.Last).ToContainTextAsync("OnInitialized");

        // Deselecting hides it again, even inside an open container.
        await App.DeselectAsync();
        await Expect(breadcrumb).ToBeHiddenAsync();

        // Alt+Up zooms out one level: the innermost open container (Main) is framed inside its
        // parent, where it draws as an icon, and is highlighted, not selected: it is the last crumb.
        await Page.Keyboard.PressAsync("Alt+ArrowUp");
        await Expect(items.Last).ToHaveTextAsync("Main");
        await Expect(App.NodeHighlight).ToBeVisibleAsync();
        await Expect(App.NodeToolbarMenu).ToBeHiddenAsync();

        // The model crumb fits the whole diagram; the highlighted node keeps its chain.
        await model.ClickAsync();
        await Expect(App.NodeLabel("Demo.sln")).ToBeVisibleAsync();
        await Expect(items.Last).ToHaveTextAsync("Main");
    }
}
