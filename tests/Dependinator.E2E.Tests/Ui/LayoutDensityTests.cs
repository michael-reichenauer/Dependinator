using Dependinator.E2E.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// The tests that change process-wide state in the Blazor Server host run alone, after the
// parallel ones (xUnit runs a collection with DisableParallelization by itself).
[CollectionDefinition(Name, DisableParallelization = true)]
public class ProcessWideStateCollection
{
    public const string Name = "Process-wide state";
}

// Settings › Layout density is stored in the browser's config, but the layout reads it from a
// static (NodeLayout.Density), which every page load in this host sets from its own config.
// Run in parallel, this flaked both ways (Nov 2026): another test's page load set the static
// back to Balanced, so the dialog (which shows the static) read Balanced after the reload, and
// the other tests laid out their diagrams Spacious while this one had it on, which moved the
// container headers that SelectContainerNodeAsync clicks.
[Collection(ProcessWideStateCollection.Name)]
public class LayoutDensityTests(ITestOutputHelper output) : E2ETestBase(output)
{
    // Set back to Balanced whatever happens here, so the tests after this one see the default.
    [E2EFact]
    public async Task LayoutDensity_ShouldPersistAcrossReload()
    {
        await App.GotoMainPageAsync();
        await App.ExpectSettingAsync("settings-density-balanced", isChecked: true);
        try
        {
            await App.ToggleSettingAsync("settings-density-spacious", expectChecked: true);
            await App.GotoMainPageAsync();
            await App.ExpectSettingAsync("settings-density-spacious", isChecked: true);
        }
        finally
        {
            await App.ToggleSettingAsync("settings-density-balanced", expectChecked: true);
        }
    }
}
