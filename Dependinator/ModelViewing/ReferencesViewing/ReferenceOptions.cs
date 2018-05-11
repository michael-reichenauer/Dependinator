using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceOptions
	{
		public bool IsIncoming { get; }
		public Node FilterNode { get; }
		public bool IsSubReference { get; }


		public ReferenceOptions(bool isIncoming, Node filterNode = null, bool isSubReference = false)
		{
			IsIncoming = isIncoming;
			FilterNode = filterNode;
			IsSubReference = isSubReference;
		}
	}
}