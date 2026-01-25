using System.Runtime.CompilerServices;

namespace DependinatorCore.Tests;

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
