using System.Runtime.CompilerServices;

namespace Dependinator.UI.Tests;

static class VerifyConfig
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // These tests use helpers from Dependinator.Reflection.Tests (ResultAssert, ItemsMock),
        // whose own module initializer also sets up Verify, which must happen before any Verify
        // runs in the process. Run it now, not whenever a test first touches that assembly: after
        // a snapshot test had run, it threw and failed every test touching it (seen in CI).
        RuntimeHelpers.RunModuleConstructor(typeof(Dependinator.Reflection.Tests.VerifyConfig).Module.ModuleHandle);

        UseSourceFileRelativeDirectory("_Snapshots");
        VerifierSettings.DontScrubGuids();
        VerifierSettings.DontScrubDateTimes();
    }
}
