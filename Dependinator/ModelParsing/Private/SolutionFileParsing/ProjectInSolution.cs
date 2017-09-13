using System;


namespace Dependinator.ModelParsing.Private.SolutionFileParsing
{
	/// <summary>
	/// Wraps the Microsoft.Build.Construction.ProjectInSolution type,
	/// which is internal type and cannot be used directly. 
	/// This class uses reflection to access the internal functionality.
	/// </summary>
	internal class ProjectInSolution
	{
		public string ProjectName { get; }
		public string ProjectType { get; }
		public string RelativePath { get; }

		public bool IsSolutionFolder => ProjectType.IsSameIgnoreCase("SolutionFolder");


		public ProjectInSolution(object instance)
		{
			// Microsoft.Build.Construction.ProjectInSolution
			ProjectName = instance.GetProperty<string>(nameof(ProjectName));
			ProjectType = instance.GetProperty<object>(nameof(ProjectType)).ToString();
			RelativePath = instance.GetProperty<string>(nameof(RelativePath));
		}
	}
}