using System.Runtime.CompilerServices;
using Dependinator.Core.Parsing.Assemblies;
using Dependinator.Core.Tests.Parsing.Utils;

namespace Dependinator.Core.Tests.Parsing.Assemblies;

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

        if (!Try(out var source, out var e, decompiler.TryGetSource(module, nodeName)))
            Assert.Fail(e.ErrorMessage);

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
        if (!Try(out var source1, out var e1, decompiler.TryGetSource(module, nodeName1)))
            Assert.Fail(e1.ErrorMessage);

        await Verify(source1.Text, extension: "cs");
        Assert.Equal(CurrentFilePath(), source1.Location.Path);
        Assert.Equal(12, source1.Location.Line);

        string nodeName2 = Reference.NodeName<DecompilerTestClass>(nameof(DecompilerTestClass.SecondFunction));
        if (!Try(out var source2, out var e2, decompiler.TryGetSource(module, nodeName2)))
            Assert.Fail(e2.ErrorMessage);
        Assert.Equal(CurrentFilePath(), source2.Location.Path);
        Assert.Equal(16, source2.Location.Line);
    }

    [Fact]
    public async Task GetNodeNameAsync()
    {
        Decompiler decompiler = new();
        var module = AssemblyHelper.GetModule<DecompilerTestClass>();

        // // Find first type in specified file
        var fileLocation1 = decompiler.TryGetSource(module, Reference.NodeName<DecompilerTestClass>());
        Assert.False(fileLocation1.IsResultError);
        var isFound11 = decompiler.TryGetNodeNameForFileLocation(
            module,
            fileLocation1.GetResultValue().Location,
            out var nodeName1
        );
        Assert.True(isFound11);
        Assert.Equal(Reference.NodeName<DecompilerTestClass>(), nodeName1);

        // Find FirstFunction() in specified file
        var fileLocation2 = decompiler.TryGetSource(module, Reference.NodeName<DecompilerTests>());
        Assert.False(fileLocation2.IsResultError);
        var isFound22 = decompiler.TryGetNodeNameForFileLocation(
            module,
            fileLocation2.GetResultValue().Location,
            out var nodeName2
        );
        Assert.True(isFound22);
        Assert.Equal(Reference.NodeName<DecompilerTests>(), nodeName2);
    }

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
