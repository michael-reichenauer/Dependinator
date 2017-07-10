using Dependinator.Utils;

namespace Dependinator.Modeling
{
	internal class Link : Equatable<Link>
	{
		public Link(NodeId sourceId, NodeId targetId)
		{
			Id = new LinkId(sourceId, targetId);
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