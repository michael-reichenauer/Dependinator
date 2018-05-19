using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceOptions
	{
		public bool IsSource { get; }
		public Node SourceFilter { get; }
		public Node TargetFilter { get; }


		public ReferenceOptions(
			bool isSource,
			Node sourceFilter,
			Node targetFilter)
		{
			IsSource = isSource;
			SourceFilter = sourceFilter;
			TargetFilter = targetFilter;
		}
	}
}