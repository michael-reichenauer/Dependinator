using System.Collections.Generic;
using System.Linq;
using Dependinator.Modeling;

namespace Dependinator.ModelViewing.Private
{
	internal class Links
	{
		private readonly Dictionary<LinkId, Link> links = new Dictionary<LinkId, Link>();

		private readonly Dictionary<NodeId, List<LinkId>> nodeLinks =
			new Dictionary<NodeId, List<LinkId>>();


		public IEnumerable<Link> NodeLinks(NodeId nodeId) => nodeLinks[nodeId].Select(Link);

		public Link Link(LinkId linkId) => links[linkId];
	}
}