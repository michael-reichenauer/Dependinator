using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Sync;

// Signed-in cloud-sync UI flow. Runs only under `./scripts/e2e -s` (Azurite + Functions on 7071 +
// the test JWKS server). Stubs Clerk via AppPage.StubClerkSignInAsync (no real Clerk) and
// seeds a model via SeededSyncModel, then drives the browser: click the cloud button to sign
// in, and verify the signed-in UI reflects the authenticated session and lists the seeded
// model. This exercises the whole Clerk-stub -> Bearer token -> /api -> UI chain that the
// API-only SyncTests cannot reach.
public class SignedInUiTests : E2ETestBase, IClassFixture<SeededSyncModel>
{
    static readonly Regex Connecting = new("cloud-connecting");

    // SeededSyncModel is a class fixture: xUnit seeds the model (IAsyncLifetime) before this
    // test. Signing in as that same user is what makes the model appear in the cloud list.
    public SignedInUiTests(SeededSyncModel seededModel, ITestOutputHelper output)
        : base(output) => _ = seededModel;

    [SyncFact]
    public async Task CloudSignIn_ShouldAuthenticateAndListSeededModel()
    {
        // Stub Clerk so the cloud-button click signs in as the seed user, which makes
        // /api/models return the seeded model. The stub stays signed out until then, so
        // the app cannot authenticate (and auto-sync) in the background before the click.
        await App.StubClerkSignInAsync(sub: SeededSyncModel.UserSub);
        await App.GotoMainPageAsync();

        // Wait out the transient "connecting" state — the cloud button silently ignores
        // clicks while the initial auth probe is still running.
        await Expect(App.CloudButton).Not.ToHaveClassAsync(Connecting, new() { Timeout = 15_000 });

        // The cloud button opens the status popover; its "Enable device sync" button signs in
        // (clerkSignIn resolves as soon as the stubbed Clerk.openSignIn reports the user).
        await App.CloudButton.ClickAsync();
        await Page.GetByTestId("cloud-sign-in").ClickAsync();

        // Login is async (Functions round-trip). The popover then shows the account and offers
        // Sign out; Expect retries until it does.
        await App.CloudButton.ClickAsync();
        await Expect(Page.GetByTestId("cloud-account")).ToContainTextAsync("Signed in as", new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("cloud-sign-out")).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");

        // The merged model list (Models submenu) shows the seeded cloud model by name (its
        // presence proves the cloud model list was fetched and merged in).
        await App.OpenSubMenuItemAsync("menu-models", "menu-new-model");
        await Expect(App.MenuItem("menu-model-item").Filter(new() { HasText = "seed-model.sln" })).ToBeVisibleAsync();
    }
}
