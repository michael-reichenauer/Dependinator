using System;
using System.Collections;
using Dependinator.Utils.Reflection;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.SolutionParsing.Private
{
    /// <summary>
    ///     Wraps the Microsoft.Build.Construction.ProjectInSolution type,
    ///     which is internal type and cannot be used directly.
    ///     This class uses reflection to access the internal functionality.
    /// </summary>
    internal class VisualStudioProjectInSolution
    {
        public VisualStudioProjectInSolution(object instance)
        {
            Project = instance;
            UniqueProjectName = instance.GetField<string>("uniqueProjectName");
            ProjectName = instance.GetProperty<string>(nameof(ProjectName));
            ProjectType = instance.GetProperty<object>(nameof(ProjectType)).ToString();
            RelativePath = instance.GetProperty<string>(nameof(RelativePath));
            IsIncludedDebug = GetIncludedInDebug(instance);
        }


        public string ProjectName { get; }
        public string ProjectType { get; }
        public string RelativePath { get; }
        public string UniqueProjectName { get; }
        public object Project { get; }
        public bool IsSolutionFolder => ProjectType.IsSameIgnoreCase("SolutionFolder");
        public bool IsIncludedDebug { get; }


        private bool GetIncludedInDebug(object instance)
        {
            // The project configuration is a dictionary, lets try to get the Debug and check
            // if it is included in the Debug build
            object projectConfigurations;
            projectConfigurations = instance.GetField<object>(nameof(projectConfigurations));

            IEnumerable configurations = projectConfigurations as IEnumerable;

            if (configurations != null)
            {
                foreach (object item in configurations)
                {
                    // item is KeyValuePair<string, object> type
                    object Value;
                    Value = item.GetProperty<object>(nameof(Value));

                    string ConfigurationName;
                    bool IncludeInBuild;
                    ConfigurationName = Value.GetProperty<string>(nameof(ConfigurationName));
                    IncludeInBuild = Value.GetProperty<bool>(nameof(IncludeInBuild));

                    if (ConfigurationName == "Debug" && IncludeInBuild)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
