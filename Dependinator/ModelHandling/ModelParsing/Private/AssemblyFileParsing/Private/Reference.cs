using Dependinator.ModelHandling.Core;


namespace Dependinator.ModelHandling.ModelParsing.Private.AssemblyFileParsing.Private
{
	internal class Reference
	{
		public string SourceName { get; }
		public string TargetName { get; }
		public NodeType TargetType { get; }


		public Reference(string sourceName, string targetName, NodeType targetType)
		{
			SourceName = sourceName;
			TargetName = targetName;
			TargetType = targetType;
		}
	}
}