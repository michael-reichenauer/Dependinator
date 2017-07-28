using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Links
{
	internal class Link : Equatable<Link>
	{
		public Link(NodeId sourceId, NodeId targetId, LinkId linkId)
		{
			Id = linkId;
			SourceId = sourceId;
			TargetId = targetId;

			IsEqualWhen(Id);
		}

		public LinkId Id { get; }
		public NodeId TargetId { get; }
		public NodeId SourceId { get; }

		public override string ToString() => Id.ToString();
	}
}