using Dependinator.Utils;

namespace Dependinator.Modeling
{
	internal class LinkId : Equatable<LinkId>
	{
		private readonly NodeId source;
		private readonly NodeId target;


		public LinkId(NodeId source, NodeId target)
		{
			this.source = source;
			this.target = target;
			IsEqualWhen(source, target);
		}

		public override string ToString() => $"{source}->{target}";
	}
}