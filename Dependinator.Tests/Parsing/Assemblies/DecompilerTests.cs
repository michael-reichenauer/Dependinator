using DependinatorCore.Parsing.Assemblies;
using Dependinator.Tests.Parsing.Utils;
using Mono.Cecil;

namespace Dependinator.Tests.Parsing.Assemblies;

public class DecompilerTestClass
{
    public int number;

    public void FirstFunction()
    {
        int a = number;
    }

    public void SecondFunction() { }
}

public class DecompilerTests
{
    [Fact]
    public async Task GetTypeSourceAsync()
    {
        Decompiler decompiler = new();
        AssemblyDefinition assemblyDefinition = AssemblyHelper.GetAssemblyDefinition<DecompilerTestClass>();

        string nodeName = Reference.NodeName<DecompilerTestClass>();
        if (!Try(out var source, out var e, decompiler.TryGetSource(assemblyDefinition.MainModule, nodeName)))
            Assert.Fail(e.ErrorMessage);

        await Verify(source.Text, extension: "cs");
    }

    [Fact]
    public async Task GetMemberSourceAsync()
    {
        Decompiler decompiler = new();
        AssemblyDefinition assemblyDefinition = AssemblyHelper.GetAssemblyDefinition<DecompilerTestClass>();

        string nodeName = Reference.NodeName<DecompilerTestClass>(nameof(DecompilerTestClass.FirstFunction));
        if (!Try(out var source, out var e, decompiler.TryGetSource(assemblyDefinition.MainModule, nodeName)))
            Assert.Fail(e.ErrorMessage);

        await Verify(source.Text, extension: "cs");
    }
}
