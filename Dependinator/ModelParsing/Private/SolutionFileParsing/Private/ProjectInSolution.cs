using System;


namespace Dependinator.ModelParsing.Private.SolutionFileParsing.Private
{
	/// <summary>
	/// Wraps the Microsoft.Build.Construction.ProjectInSolution type,
	/// which is internal type and cannot be used directly. 
	/// This class uses reflection to access the internal functionality.
	/// </summary>
	internal class ProjectInSolution
	{
		private readonly string uniqueProjectName;

		public string ProjectName { get; }
		public string ProjectType { get; }
		public string RelativePath { get; }
		public string UniqueProjectName => uniqueProjectName;

		public bool IsSolutionFolder => ProjectType.IsSameIgnoreCase("SolutionFolder");


		public ProjectInSolution(object instance)
		{
			uniqueProjectName = instance.GetField<string>(nameof(uniqueProjectName));
			ProjectName = instance.GetProperty<string>(nameof(ProjectName));
			ProjectType = instance.GetProperty<object>(nameof(ProjectType)).ToString();
			RelativePath = instance.GetProperty<string>(nameof(RelativePath));
		}
	}
}