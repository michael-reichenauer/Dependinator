using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dependinator.Utils.Reflection;
using Microsoft.Build.Construction;


namespace Dependinator.Model.Parsing.Solutions;

/// <summary>
///     This class parses solution files ".sln" files. It wraps the internal
///     Microsoft.Build.Construction.SolutionParser and uses
///     reflection to access the internal call and its functionality.
/// </summary>
internal class VisualStudioSolutionParser
{
    private readonly object instance;


    public VisualStudioSolutionParser()
    {
        //Microsoft.Build.Construction.SolutionParser
        string typeName = "Microsoft.Build.Construction.SolutionParser";
        Assembly assembly = typeof(ProjectElement).Assembly;


        Type type = Reflection.GetType(assembly, typeName);

        instance = Reflection.Create(type);
    }


    public StreamReader SolutionReader { set => instance.SetProperty("SolutionReader", value); }


    public IReadOnlyList<VisualStudioProjectInSolution> Projects
    {
        get
        {
            object[] objects = instance.GetProperty<object[]>("Projects");

            return objects.Select(project => new VisualStudioProjectInSolution(project)).ToList();
        }
    }


    public void ParseSolution() => instance.Invoke("ParseSolution");
}

