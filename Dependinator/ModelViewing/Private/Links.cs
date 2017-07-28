using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Private
{
	internal class Links
	{
		private readonly Dictionary<LinkId, Link> links = new Dictionary<LinkId, Link>();

		private readonly Dictionary<NodeId, List<LinkId>> nodeLinks =
			new Dictionary<NodeId, List<LinkId>>();


		public IEnumerable<Link> NodeLinks(NodeId nodeId) => nodeLinks[nodeId].Select(Link);

		public Link Link(LinkId linkId) => links[linkId];

		public bool TryGetLink(LinkId linkId, out Link link) => links.TryGetValue(linkId, out link);

		public void Add(Link link)
		{
			links[link.Id] = link;
		}
	}
}