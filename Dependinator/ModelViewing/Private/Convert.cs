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

			nodes.ForEach(node => items.Add(ToModelItem(node)));
			nodes.ForEach(node => items.AddRange(ToModelItems(node.SourceLinks)));

			return items;
		}


		private static ModelItem ToModelItem(Node node) => 
			new ModelItem(ToModelNode(node), null);


		private static IEnumerable<ModelItem> ToModelItems(IEnumerable<Link> sourceLinks) =>
			sourceLinks
			.Select(ToModelLink)
			.Select(modelLink => new ModelItem(null, modelLink));


		private static ModelNode ToModelNode(Node node) => 
			new ModelNode(
			node.Name,
			node.NodeType.AsString(),
			node.ViewModel?.ItemBounds ?? node.Bounds,
			node.ViewModel?.ItemsViewModel?.ItemsCanvas?.ScaleFactor ?? node.ScaleFactor,
			node.ViewModel?.ItemsViewModel?.ItemsCanvas?.Offset ?? node.Offset,
			node.ViewModel?.Color ?? node.Color,
			node.RootGroup);


		private static ModelLink ToModelLink(Link link) => new ModelLink(
			link.Source.Name,
			link.Target.Name);
	}
}