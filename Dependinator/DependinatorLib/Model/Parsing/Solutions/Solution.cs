﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;


namespace Dependinator.Model.Parsing.Solutions;

/// <summary>
///     Represents the solution loaded from a sln file.
/// </summary>
internal class Solution
{
    readonly Lazy<IReadOnlyList<Project>> projects;


    public Solution(string solutionFilePath)
    {
        SolutionFilePath = solutionFilePath;
        SolutionDirectory = Path.GetDirectoryName(solutionFilePath) ?? "";

        projects = new Lazy<IReadOnlyList<Project>>(GetProjects);
    }


    public string SolutionFilePath { get; }

    public string SolutionDirectory { get; }


    public IReadOnlyList<string> GetDataFilePaths()
    {
        return GetSolutionProjects()
            .Select(project => project.GetOutputPath())
            .Where(path => path != null)
            .ToList();
    }


    public IReadOnlyList<Project> GetSolutionProjects() =>
        projects.Value.Where(project => !IsTestProject(project)).ToList();


    private IReadOnlyList<Project> GetProjects()
    {
        var solutionFile = SolutionFile.Parse(SolutionFilePath);
        var solutionParserProjects = solutionFile.ProjectsInOrder;

        return solutionParserProjects
              .Select(p => new Project(p, SolutionDirectory))
              .ToList();


        //     .Where(p => !p.IsSolutionFolder && p.IsIncludedDebug)
        //     .Select(p => new Project(p, SolutionDirectory))
        //     .ToList();


        // //VisualStudioSolutionParser solutionParser = new VisualStudioSolutionParser();

        // using (StreamReader streamReader = new StreamReader(SolutionFilePath))
        // {
        //     solutionParser.SolutionReader = streamReader;
        //     solutionParser.ParseSolution();
        // }

        // IReadOnlyList<VisualStudioProjectInSolution> solutionParserProjects = solutionParser.Projects;

        // return solutionParserProjects
        //     .Where(p => !p.IsSolutionFolder && p.IsIncludedDebug)
        //     .Select(p => new Project(p, SolutionDirectory))
        //     .ToList();
    }


    public override string ToString() => SolutionFilePath;


    private bool IsTestProject(Project project)
    {
        if (project.ProjectName.EndsWith("Test") || project.ProjectName.EndsWith("Tests"))
        {
            string name = project.ProjectName.Substring(0, project.ProjectName.Length - 4);

            if (projects.Value.Any(p => p.ProjectName == name))
            {
                return true;
            }
        }

        return false;
    }
}

