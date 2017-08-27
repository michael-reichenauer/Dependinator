using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelParsing;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Private
{
	internal class Convert
	{
		public static List<DataItem> ToDataItems(IReadOnlyList<Node> nodes)
		{
			List<DataItem> items = new List<DataItem>();

			nodes.ForEach(node => items.Add(ToNodeItem(node)));
			nodes.ForEach(node => items.AddRange(ToLinkItems(node)));

			return items;
		}


		private static DataNode ToDataNode(Node node) => new DataNode(
			node.Name.AsString(),
			node.NodeType.AsString(),
			node.ViewModel?.ItemBounds ?? node.Bounds,
			node.ViewModel?.ItemsViewModel?.ItemsCanvas?.Scale ?? node.Scale,
			node.ViewModel?.ItemsViewModel?.ItemsCanvas?.Offset ?? node.Offset,
			node.ViewModel?.Color ?? node.Color);


		private static DataLink ToDataLink(Link link) => new DataLink(
			link.Source.Name.AsString(), 
			link.Target.Name.AsString());



		private static IEnumerable<DataItem> ToLinkItems(Node node) =>
			ToDataLinks(node).Select(ToDataItem);

		private static DataItem ToDataItem(DataLink dataLink) => new DataItem(null, dataLink);

		private static IEnumerable<DataLink> ToDataLinks(Node node) =>
			node.SourceLinks.Select(ToDataLink);


		private static DataItem ToNodeItem(Node node) => ToDataItem(node);

		private static DataItem ToDataItem(Node node) => new DataItem(ToDataNode(node), null);
	}
}