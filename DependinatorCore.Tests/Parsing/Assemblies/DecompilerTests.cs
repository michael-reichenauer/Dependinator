using System.Runtime.CompilerServices;
using DependinatorCore.Parsing.Assemblies;
using DependinatorCore.Tests.Parsing.Utils;
using Mono.Cecil;

namespace DependinatorCore.Tests.Parsing.Assemblies;

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
        Assert.Equal(CurrentFilePath(), source.Path);
        Assert.Equal(13, source.LineNumber); // Note: Line number is of first member function in type !!
    }

    [Fact]
    public async Task GetMemberSourceAsync()
    {
        Decompiler decompiler = new();
        AssemblyDefinition assemblyDefinition = AssemblyHelper.GetAssemblyDefinition<DecompilerTestClass>();

        string nodeName1 = Reference.NodeName<DecompilerTestClass>(nameof(DecompilerTestClass.FirstFunction));
        if (!Try(out var source1, out var e1, decompiler.TryGetSource(assemblyDefinition.MainModule, nodeName1)))
            Assert.Fail(e1.ErrorMessage);

        await Verify(source1.Text, extension: "cs");
        Assert.Equal(CurrentFilePath(), source1.Path);
        Assert.Equal(13, source1.LineNumber);

        string nodeName2 = Reference.NodeName<DecompilerTestClass>(nameof(DecompilerTestClass.SecondFunction));
        if (!Try(out var source2, out var e2, decompiler.TryGetSource(assemblyDefinition.MainModule, nodeName2)))
            Assert.Fail(e2.ErrorMessage);
        Assert.Equal(CurrentFilePath(), source2.Path);
        Assert.Equal(17, source2.LineNumber);
    }

    [Fact]
    public async Task GetNodeNameAsync()
    {
        Decompiler decompiler = new();
        AssemblyDefinition assemblyDefinition = AssemblyHelper.GetAssemblyDefinition<DecompilerTestClass>();
        var assemblyTypes = GetAssemblyTypes(assemblyDefinition);

        // Find first type in specified file
        var isFound = decompiler.TryGetNodeNameForSourceFile(assemblyTypes, CurrentFilePath(), out var nodeName);

        Assert.True(isFound);
        Assert.Equal(Reference.NodeName<DecompilerTestClass>(), nodeName);
    }

    IEnumerable<TypeDefinition> GetAssemblyTypes(AssemblyDefinition assemblyDefinition)
    {
        return assemblyDefinition.MainModule.Types.Where(type =>
            !Name.IsCompilerGenerated(type.Name) && !Name.IsCompilerGenerated(type.DeclaringType?.Name ?? "")
        );
    }

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
