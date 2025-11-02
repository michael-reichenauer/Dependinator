using System;
using Dependinator.Parsing.Assemblies;
using Dependinator.Tests.Parsing.Utils;
using Mono.Cecil;

namespace Dependinator.Tests.Parsing.Assemblies;

public class DecompilerTestClass
{
    public int number;

    public void FirstFunction()
    {
        var a = number;
    }

    public void SecondFunction() { }
}

public class DecompilerTests
{
    [Fact]
    public async Task GetSourceAsync()
    {
        var parameters = new ReaderParameters { AssemblyResolver = new ParsingAssemblyResolver(), ReadSymbols = false };
        var assemblyPath = typeof(DecompilerTestClass).Assembly.Location;
        var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, parameters);

        Decompiler decompiler = new();
        decompiler.TryGetSource(assemblyDefinition.MainModule, Reference.NodeName<DecompilerTestClass>());
    }
}
