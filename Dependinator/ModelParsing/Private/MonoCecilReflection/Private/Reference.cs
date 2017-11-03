namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class Reference
	{
		public string SourceName { get; }
		public string TargetName { get; }
		public string TargetType { get; }


		public Reference(string sourceName, string targetName, string targetType)
		{
			SourceName = sourceName;
			TargetName = targetName;
			TargetType = targetType;
		}
	}
}