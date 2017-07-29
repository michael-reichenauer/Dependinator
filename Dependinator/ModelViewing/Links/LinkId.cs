using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Links
{
	internal class LinkId : Equatable<LinkId>
	{
		private readonly NodeId source;
		private readonly NodeId target;


		public LinkId(NodeId source, NodeId target)
		{
			this.source = source;
			this.target = target;
			IsEqualWhenSame(source, target);
		}

		public override string ToString() => $"{source}->{target}";
	}
}