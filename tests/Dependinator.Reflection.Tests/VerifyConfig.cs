using System.Runtime.CompilerServices;

namespace Dependinator.Reflection.Tests;

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
