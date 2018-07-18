﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dependinator.Utils.Reflection;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.SolutionParsing.Private
{

	/// <summary>
	/// This class parses solution files ".sln" files. It wraps the internal
	/// Microsoft.Build.Construction.SolutionParser and uses
	/// reflection to access the internal call and its functionality.
	/// </summary>
	internal class VisualStudioSolutionParser
	{
		private readonly object instance;


		public VisualStudioSolutionParser()
		{
			//Microsoft.Build.Construction.SolutionParser
			string typeName = "Microsoft.Build.Construction.SolutionParser";
			Assembly assembly = typeof(Microsoft.Build.Construction.ProjectElement).Assembly;

			
			Type type = Reflection.GetType(assembly, typeName);

			instance = Reflection.Create(type);
		}


		public StreamReader SolutionReader
		{
			set => instance.SetProperty(nameof(SolutionReader), value);
		}


		public IReadOnlyList<VisualStudioProjectInSolution> Projects
		{
			get
			{
				object[] objects = instance.GetProperty<object[]>(nameof(Projects));

				return objects.Select(project => new VisualStudioProjectInSolution(project)).ToList();
			}
		}


		public void ParseSolution() => instance.Invoke(nameof(ParseSolution));
	}
}