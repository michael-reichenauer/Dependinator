using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceOptions
	{
		public bool IsNodes { get; }
		public bool IsIncoming { get; }
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
}