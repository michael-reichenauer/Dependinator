using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises display names through the browser: alias the demo's Externals node from the node
// menu, see the new label while the real name stays in the tooltip, survive a reload, find it by
// the alias in the search, and remove the alias again from the right-click menu.
public class AliasTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Alias_ShouldReplaceTheLabel_KeepTheRealNameInTheTitle_AndPersist()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.EnableEditModeAsync();

        await App.SelectNodeByFullNameAsync("Externals");
        await App.ClickNodeMenuItemAsync("node-menu-alias");
        await SaveAliasAsync("Third parties");

        await Expect(App.NodeLabel("Third parties")).ToBeVisibleAsync();
        await Expect(App.Node("Externals")).ToBeVisibleAsync(); // the real name heads the tooltip

        // The debounced save writes to IndexedDB; give it a moment, then reload.
        await Page.WaitForTimeoutAsync(1500);
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await Expect(App.NodeLabel("Third parties")).ToBeVisibleAsync();
        await Expect(App.Node("Externals")).ToBeVisibleAsync();

        // Search finds the node by its display name and still shows the real name.
        SearchDialog search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Third");
        await Expect(search.ResultByFullName("Externals")).ToBeVisibleAsync();
        await search.CloseAsync();
    }

    [E2EFact]
    public async Task Alias_ShouldBeRemovable_FromTheContextMenu()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.EnableEditModeAsync();

        await App.SelectNodeByFullNameAsync("Externals");
        await App.ClickNodeMenuItemAsync("node-menu-alias");
        await SaveAliasAsync("Third parties");
        await Expect(App.NodeLabel("Third parties")).ToBeVisibleAsync();

        // Right-click the node's icon: the node menu offers the display-name editor.
        LocatorBoundingBoxResult box =
            await App.Node("Externals").BoundingBoxAsync() ?? throw new InvalidOperationException("Node not rendered.");
        await Page.Mouse.ClickAsync(
            box.X + box.Width / 2,
            box.Y + box.Height / 2,
            new() { Button = MouseButton.Right }
        );
        await Page.GetByTestId("context-menu-alias").ClickAsync();
        await Expect(Page.GetByTestId("node-alias-text")).ToBeVisibleAsync();
        await Page.GetByTestId("node-alias-clear").ClickAsync();

        await Expect(App.NodeLabel("Third parties")).ToHaveCountAsync(0);
        await Expect(App.NodeLabel("Externals")).ToBeVisibleAsync();
    }

    async Task SaveAliasAsync(string alias)
    {
        ILocator field = Page.GetByTestId("node-alias-text");
        await Expect(field).ToBeVisibleAsync();
        await App.FillReliablyAsync(field, alias);
        await Page.GetByTestId("node-alias-save").ClickAsync();
    }
}
