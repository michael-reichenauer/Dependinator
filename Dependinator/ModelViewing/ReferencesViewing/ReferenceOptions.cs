using System.Collections.Generic;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceOptions
	{
		public bool IsNodes { get; }
		public bool IsIncoming { get; }
		public bool IsOutgoing => !IsIncoming;
		public Node FilterNode { get; }
		public bool IsSubReference { get; }


		public ReferenceOptions(
			bool isNodes,
			bool isIncoming, 
			Node filterNode = null, 
			bool isSubReference = false)
		{
			IsNodes = isNodes;
			IsIncoming = isIncoming;
			FilterNode = filterNode;
			IsSubReference = isSubReference;
		}
	}

	internal class ReferenceOptions2
	{
		public IEnumerable<Line> Lines { get; }
		public bool IsSource { get; }
		public Node SourceFilter { get; }
		public Node TargetFilter { get; }


		public ReferenceOptions2(
			IEnumerable<Line> lines,
			bool isSource,
			Node sourceFilter,
			Node targetFilter)
		{
			Lines = lines;
			IsSource = isSource;
			SourceFilter = sourceFilter;
			TargetFilter = targetFilter;
		}
	}
}