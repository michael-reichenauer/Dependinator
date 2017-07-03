using Dependinator.Utils;

namespace Dependinator.Modeling
{
	internal class Link2 : Equatable<Link2>
	{
		public Link2(NodeId sourceId, NodeId targetId)
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