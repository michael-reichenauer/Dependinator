using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceOptions
	{
		public bool IsIncoming { get; }
		public Node FilterNode { get; }


		public ReferenceOptions(bool isIncoming, Node filterNode = null)
		{
			IsIncoming = isIncoming;
			FilterNode = filterNode;
		}
	}
}