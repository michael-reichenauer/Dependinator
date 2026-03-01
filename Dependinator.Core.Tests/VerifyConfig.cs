using System.Runtime.CompilerServices;

namespace Dependinator.Core.Tests;

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
