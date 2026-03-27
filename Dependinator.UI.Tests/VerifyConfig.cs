using System.Runtime.CompilerServices;

namespace Dependinator.UI.Tests;

static class VerifyConfig
{
    [ModuleInitializer]
    public static void Initialize()
    {
        UseSourceFileRelativeDirectory("_Snapshots");
        VerifierSettings.DontScrubGuids();
        VerifierSettings.DontScrubDateTimes();
    }
}
