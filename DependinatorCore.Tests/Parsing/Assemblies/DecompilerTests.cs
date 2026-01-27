using System.Runtime.CompilerServices;
using DependinatorCore.Parsing;
using DependinatorCore.Parsing.Assemblies;
using DependinatorCore.Tests.Parsing.Utils;

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
        var module = AssemblyHelper.GetModule<DecompilerTestClass>();

        string nodeName = Reference.NodeName<DecompilerTestClass>();

        if (!Try(out var source, out var e, decompiler.TryGetSource(module, nodeName)))
            Assert.Fail(e.ErrorMessage);

        await Verify(source.Text, extension: "cs");
        Assert.Equal(CurrentFilePath(), source.Location.Path);
        Assert.Equal(13, source.Location.Line); // Note: Line number is of first member function in type !!
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
        Assert.Equal(13, source1.Location.Line);

        string nodeName2 = Reference.NodeName<DecompilerTestClass>(nameof(DecompilerTestClass.SecondFunction));
        if (!Try(out var source2, out var e2, decompiler.TryGetSource(module, nodeName2)))
            Assert.Fail(e2.ErrorMessage);
        Assert.Equal(CurrentFilePath(), source2.Location.Path);
        Assert.Equal(17, source2.Location.Line);
    }

    [Fact]
    public async Task GetNodeNameAsync()
    {
        Decompiler decompiler = new();
        var module = AssemblyHelper.GetModule<DecompilerTestClass>();

        // Find first type in specified file
        var fileLocation = new FileLocation(CurrentFilePath(), 0);
        var isFound = decompiler.TryGetNodeNameForSourceFile(module, fileLocation, out var nodeName);

        Assert.True(isFound);
        Assert.Equal(Reference.NodeName<DecompilerTestClass>(), nodeName);
    }

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
