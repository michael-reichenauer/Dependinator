using System.Reflection;
using NetArchTest.Rules;

namespace Dependinator.Architecture.Tests;

// Guards the solution's layering direction against future drift. The dependency direction is
// Hosts -> UI -> Core -> Shared (with Roslyn -> Core). Today the project references already make
// upward dependencies impossible to compile, but these tests fail loudly if someone later adds
// such a reference (e.g. Core taking a dependency on UI for a "quick fix").
public class LayeringTests
{
    static readonly Assembly CoreAssembly = typeof(Dependinator.Core.RootClass).Assembly;
    static readonly Assembly UiAssembly = typeof(Dependinator.UI.RootClass).Assembly;
    static readonly Assembly SharedAssembly = typeof(global::Shared.CloudUserInfo).Assembly;
    static readonly Assembly RoslynAssembly = Assembly.Load("Dependinator.Roslyn");

    static readonly string[] HostAssemblies = ["Dependinator.Web", "Dependinator.Wasm", "Dependinator.Lsp", "Api"];

    [Fact]
    public void Core_ShouldNotDependOnUiOrRoslynOrHosts()
    {
        AssertNoDependencyOn(CoreAssembly, ["Dependinator.UI", "Dependinator.Roslyn", .. HostAssemblies]);
    }

    [Fact]
    public void Core_ShouldNotDependOnUiFrameworks()
    {
        AssertNoDependencyOn(CoreAssembly, ["MudBlazor", "Microsoft.AspNetCore"]);
    }

    [Fact]
    public void Ui_ShouldNotDependOnHosts()
    {
        AssertNoDependencyOn(UiAssembly, HostAssemblies);
    }

    [Fact]
    public void Roslyn_ShouldNotDependOnUiOrHosts()
    {
        AssertNoDependencyOn(RoslynAssembly, ["Dependinator.UI", .. HostAssemblies]);
    }

    [Fact]
    public void Shared_ShouldNotDependOnOtherDependinatorProjects()
    {
        AssertNoDependencyOn(
            SharedAssembly,
            ["Dependinator.Core", "Dependinator.UI", "Dependinator.Roslyn", .. HostAssemblies]
        );
    }

    static void AssertNoDependencyOn(Assembly assembly, string[] forbiddenDependencies)
    {
        TestResult result = Types
            .InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        string failing = string.Join(", ", result.FailingTypeNames ?? []);
        Assert.True(
            result.IsSuccessful,
            $"{assembly.GetName().Name} must not depend on [{string.Join(", ", forbiddenDependencies)}]. "
                + $"Offending types: {failing}"
        );
    }
}
