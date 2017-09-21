using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelParsing;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Private
{
	internal class Convert
	{
		public static List<ModelItem> ToDataItems(IReadOnlyList<Node> nodes)
		{
			List<ModelItem> items = new List<ModelItem>();

			nodes.ForEach(node => items.Add(ToNodeItem(node)));
			nodes.ForEach(node => items.AddRange(ToLinkItems(node)));

			return items;
		}


		private static ModelNode ToDataNode(Node node) => new ModelNode(
			node.Name,
			node.NodeType.AsString(),
			node.ViewModel?.ItemBounds ?? node.Bounds,
			node.ViewModel?.ItemsViewModel?.ItemsCanvas?.Scale ?? node.Scale,
			node.ViewModel?.ItemsViewModel?.ItemsCanvas?.Offset ?? node.Offset,
			node.ViewModel?.Color ?? node.Color,
			node.Group);


		private static ModelLink ToDataLink(Link link) => new ModelLink(
			link.Source.Name,
			link.Target.Name);



		private static IEnumerable<ModelItem> ToLinkItems(Node node) =>
			ToDataLinks(node).Select(ToDataItem);

		private static ModelItem ToDataItem(ModelLink modelLink) => new ModelItem(null, modelLink);

		private static IEnumerable<ModelLink> ToDataLinks(Node node) =>
			node.SourceLinks.Select(ToDataLink);


		private static ModelItem ToNodeItem(Node node) => ToDataItem(node);

		private static ModelItem ToDataItem(Node node) => new ModelItem(ToDataNode(node), null);
	}
}