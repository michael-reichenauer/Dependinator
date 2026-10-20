using System.Reflection;
using NetArchTest.Rules;

namespace Dependinator.Architecture.Tests;

// Guards the solution's layering direction against future drift. The dependency direction is
// Hosts -> UI -> Core -> Shared (with Roslyn -> Core, Bicep -> Core, Reflection -> Core and Claude -> Core). Today the project references already make
// upward dependencies impossible to compile, but these tests fail loudly if someone later adds
// such a reference (e.g. Core taking a dependency on UI for a "quick fix").
public class LayeringTests
{
    static readonly Assembly CoreAssembly = typeof(Dependinator.Core.RootClass).Assembly;
    static readonly Assembly UiAssembly = typeof(Dependinator.UI.RootClass).Assembly;
    static readonly Assembly SharedAssembly = typeof(global::Shared.CloudUserInfo).Assembly;
    static readonly Assembly RoslynAssembly = Assembly.Load("Dependinator.Roslyn");
    static readonly Assembly ReflectionAssembly = Assembly.Load("Dependinator.Reflection");
    static readonly Assembly BicepAssembly = Assembly.Load("Dependinator.Bicep");
    static readonly Assembly ClaudeAssembly = Assembly.Load("Dependinator.Claude");

    static readonly string[] HostAssemblies = ["Dependinator.Web", "Dependinator.Wasm", "Dependinator.Lsp", "Api"];

    [Fact]
    public void Core_ShouldNotDependOnUiOrRoslynOrHosts()
    {
        AssertNoDependencyOn(
            CoreAssembly,
            [
                "Dependinator.UI",
                "Dependinator.Roslyn",
                "Dependinator.Bicep",
                "Dependinator.Reflection",
                "Dependinator.Claude",
                .. HostAssemblies,
            ]
        );
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
        AssertNoDependencyOn(RoslynAssembly, ["Dependinator.UI", "Dependinator.Bicep", .. HostAssemblies]);
    }

    [Fact]
    public void Bicep_ShouldNotDependOnUiOrHosts()
    {
        AssertNoDependencyOn(BicepAssembly, ["Dependinator.UI", "Dependinator.Roslyn", .. HostAssemblies]);
    }

    [Fact]
    public void Claude_ShouldNotDependOnUiOrParsersOrHosts()
    {
        AssertNoDependencyOn(
            ClaudeAssembly,
            ["Dependinator.UI", "Dependinator.Roslyn", "Dependinator.Bicep", .. HostAssemblies]
        );
    }

    // Core and UI run in the browser too (the Wasm host), so the Anthropic SDK stays out of them:
    // the UI reaches Claude through Core's IClaudeService over RPC.
    [Fact]
    public void CoreAndUi_ShouldNotDependOnTheClaudeSdk()
    {
        AssertNoDependencyOn(CoreAssembly, ["Dependinator.Claude", "Anthropic"]);
        AssertNoDependencyOn(UiAssembly, ["Dependinator.Claude", "Anthropic"]);
    }

    [Fact]
    public void Reflection_ShouldNotDependOnUiOrHosts()
    {
        AssertNoDependencyOn(ReflectionAssembly, ["Dependinator.UI", .. HostAssemblies]);
    }

    [Fact]
    public void Shared_ShouldNotDependOnOtherDependinatorProjects()
    {
        AssertNoDependencyOn(
            SharedAssembly,
            [
                "Dependinator.Core",
                "Dependinator.UI",
                "Dependinator.Roslyn",
                "Dependinator.Bicep",
                "Dependinator.Reflection",
                "Dependinator.Claude",
                .. HostAssemblies,
            ]
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
