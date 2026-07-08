using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Sync;

// Signed-in cloud-sync UI flow. Runs only under `./e2e -s` (Azurite + Functions on 7071 +
// the test JWKS server). Stubs Clerk via AppPage.SignInAsTestUserAsync (no real Clerk) and
// seeds a model via SeededSyncModel, then drives the browser: click the cloud button to sign
// in, and verify the signed-in UI reflects the authenticated session and lists the seeded
// model. This exercises the whole Clerk-stub -> Bearer token -> /api -> UI chain that the
// API-only SyncTests cannot reach.
public class SignedInUiTests : E2ETestBase, IClassFixture<SeededSyncModel>
{
    static readonly Regex Disabled = new("mud-disabled");

    // SeededSyncModel is a class fixture: xUnit seeds the model (IAsyncLifetime) before this
    // test. Signing in as that same user is what makes the model appear in the cloud list.
    public SignedInUiTests(SeededSyncModel seededModel, ITestOutputHelper output)
        : base(output) => _ = seededModel;

    [SyncFact]
    public async Task CloudSignIn_ShouldAuthenticateAndListSeededModel()
    {
        // Appear signed in as the seed user so /api/models returns the seeded model.
        await App.SignInAsTestUserAsync(sub: SeededSyncModel.UserSub);
        await App.GotoMainPageAsync();

        // The cloud button starts not-authenticated; clicking it signs in (clerkSignIn
        // resolves immediately because the stubbed Clerk already reports a user).
        await App.CloudButton.ClickAsync();

        // Login is async (Functions round-trip). The cloud tooltip flips from "Cloud not
        // signed in" to "Cloud signed in as <email>" once it completes; hovering keeps the
        // tooltip open while Expect retries.
        await App.CloudButton.HoverAsync();
        await Expect(Page.GetByText(new Regex("signed in as"))).ToBeVisibleAsync(new() { Timeout = 30_000 });

        // The app menu now reflects an authenticated session (Logout and Cloud Models both
        // live inside the Models submenu, so hover it to expand the flyout) ...
        await App.Menu.ClickAsync();
        await App.MenuItem("menu-models").HoverAsync();
        await Expect(App.MenuItem("menu-logout")).ToBeVisibleAsync();

        // ... and the Cloud Models submenu is enabled (i.e. the seeded model was listed) ...
        await Expect(App.MenuItem("menu-cloud-models")).Not.ToHaveClassAsync(Disabled);

        // ... and opening it shows the seeded model by name.
        await App.MenuItem("menu-cloud-models").HoverAsync();
        await Expect(Page.GetByText("seed-model.sln")).ToBeVisibleAsync();
    }
}
