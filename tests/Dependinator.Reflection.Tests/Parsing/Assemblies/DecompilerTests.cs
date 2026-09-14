using System.Runtime.CompilerServices;
using Dependinator.Reflection.Parsing.Assemblies;
using Dependinator.Reflection.Tests.Parsing.Utils;

namespace Dependinator.Reflection.Tests.Parsing.Assemblies;

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
        var module = AssemblyHelper.GetModule<DecompilerTestClass>();

        string nodeName = Reference.NodeName<DecompilerTestClass>();

        var source = AssertOk(decompiler.TryGetSource(module, nodeName));

        await Verify(source.Text, extension: "cs");
        Assert.Equal(CurrentFilePath(), source.Location.Path);
        Assert.Equal(12, source.Location.Line); // Note: Line number is of first member function in type !!
    }

    [Fact]
    public async Task GetMemberSourceAsync()
    {
        Decompiler decompiler = new();
        var module = AssemblyHelper.GetModule<DecompilerTestClass>();
        string nodeName1 = Reference.NodeName<DecompilerTestClass>(nameof(DecompilerTestClass.FirstFunction));
        var source1 = AssertOk(decompiler.TryGetSource(module, nodeName1));

        await Verify(source1.Text, extension: "cs");
        Assert.Equal(CurrentFilePath(), source1.Location.Path);
        Assert.Equal(12, source1.Location.Line);

        string nodeName2 = Reference.NodeName<DecompilerTestClass>(nameof(DecompilerTestClass.SecondFunction));
        var source2 = AssertOk(decompiler.TryGetSource(module, nodeName2));
        Assert.Equal(CurrentFilePath(), source2.Location.Path);
        Assert.Equal(16, source2.Location.Line);
    }

    [Fact]
    public async Task GetNodeNameAsync()
    {
        Decompiler decompiler = new();
        var module = AssemblyHelper.GetModule<DecompilerTestClass>();

        // // Find first type in specified file
        var fileLocation1 = AssertOk(decompiler.TryGetSource(module, Reference.NodeName<DecompilerTestClass>()));
        var isFound11 = decompiler.TryGetNodeNameForFileLocation(module, fileLocation1.Location, out var nodeName1);
        Assert.True(isFound11);
        Assert.Equal(Reference.NodeName<DecompilerTestClass>(), nodeName1);

        // Find FirstFunction() in specified file
        var fileLocation2 = AssertOk(decompiler.TryGetSource(module, Reference.NodeName<DecompilerTests>()));
        var isFound22 = decompiler.TryGetNodeNameForFileLocation(module, fileLocation2.Location, out var nodeName2);
        Assert.True(isFound22);
        Assert.Equal(Reference.NodeName<DecompilerTests>(), nodeName2);
    }

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
