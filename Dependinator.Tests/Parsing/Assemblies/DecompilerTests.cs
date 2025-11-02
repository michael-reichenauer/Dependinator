using System;
using Dependinator.Models;
using Dependinator.Parsing.Assemblies;
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

        var parameters = new ReaderParameters { AssemblyResolver = new ParsingAssemblyResolver(), ReadSymbols = true };
        var assemblyPath = typeof(DecompilerTestClass).Assembly.Location;
        var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, parameters);

        string nodeName = Reference.NodeName<DecompilerTestClass>();
        if (!Try(out var source, out var e, decompiler.TryGetSource(assemblyDefinition.MainModule, nodeName)))
            Assert.Fail(e.ErrorMessage);
        var sourceText = source.Text.Replace("\t", "    ");

        await Verify(sourceText, extension: "cs");
    }
}
