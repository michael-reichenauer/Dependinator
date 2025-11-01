using System.Runtime.CompilerServices;

namespace Dependinator.Tests;

static class VerifyConfig
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifyXunit.Verifier.DerivePathInfo(
            (sourceFile, _, type, method) =>
            {
                var directory = Path.Combine(Path.GetDirectoryName(sourceFile)!, "_Snapshots");
                Directory.CreateDirectory(directory);
                return new(directory, type.Name, method.Name);
            }
        );
    }
}
