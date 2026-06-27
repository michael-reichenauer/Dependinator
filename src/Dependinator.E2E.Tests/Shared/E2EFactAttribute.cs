using Xunit;

namespace Dependinator.E2E.Tests.Shared;

// E2E tests need a running app and installed browsers, so they are skipped in
// plain 'dotnet test' runs and only execute when E2E=1 (set by the ./e2e script).
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("E2E") != "1")
        {
            Skip = "E2E tests are skipped unless E2E=1. Run them via ./e2e";
        }
    }
}
