using System.Collections.Generic;
using System.Linq;
using Dependinator.Modeling;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Private
{
	internal class Convert
	{
		public static List<DataItem> ToDataItems(IReadOnlyList<Node> nodes)
		{
			List<DataItem> items = new List<DataItem>();

			nodes.ForEach(node => items.Add(ToNodeDataItem(node)));
			nodes.ForEach(node => items.AddRange(ToLinkDataItems(node)));

			return items;
		}


		private static IEnumerable<DataItem> ToLinkDataItems(Node node) =>
			ToDataItems(ToDataLinks(node));


		private static DataItem ToNodeDataItem(Node node) => new DataItem(ToDataNode(node), null);


		private static IEnumerable<DataItem> ToDataItems(IEnumerable<DataLink> dataLinks) =>
			dataLinks.Select(dataLink => new DataItem(null, dataLink));


		private static DataNode ToDataNode(Node node) => new DataNode(
			node.Name.AsString(),
			node.NodeType.AsString(),
			node.Bounds,
			node.Scale,
			node.Offset,
			node.Color);


		private static IEnumerable<DataLink> ToDataLinks(Node node) =>
			node.SourceLinks.Select(ToDataLink);


		private static DataLink ToDataLink(Link link) =>
			new DataLink(link.Source.Name.AsString(), link.Target.Name.AsString());
	}
}