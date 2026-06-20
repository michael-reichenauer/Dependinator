using Xunit;

namespace Dependinator.E2E.Tests;

// Cloud-sync UI tests additionally need the Azure Functions API (port 7071) and
// Azurite running, so they only execute when E2E_SYNC=1 (set by './e2e -s').
// They are also subject to the E2E=1 gate inherited from a plain e2e run.
public sealed class SyncFactAttribute : FactAttribute
{
    public SyncFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("E2E") != "1")
        {
            Skip = "E2E tests are skipped unless E2E=1. Run them via ./e2e";
        }
        else if (Environment.GetEnvironmentVariable("E2E_SYNC") != "1")
        {
            Skip = "Sync tests need the cloud-sync stack. Run them via ./e2e -s";
        }
    }
}
