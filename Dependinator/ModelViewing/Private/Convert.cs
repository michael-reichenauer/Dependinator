using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelParsing;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Private
{
	internal class Convert
	{
		public static List<IModelItem> ToDataItems(IReadOnlyList<Node> nodes)
		{
			List<IModelItem> items = new List<IModelItem>();

			nodes.ForEach(node => items.Add(ToModelNode(node)));
			nodes.ForEach(node => items.AddRange(ToModelLines(node.SourceLines)));
			nodes.ForEach(node => items.AddRange(ToModelLinks(node.SourceLinks)));

			return items;
		}


		private static IEnumerable<ModelLine> ToModelLines(IEnumerable<Line> lines) =>
			lines.Select(line => new ModelLine(
				line.Source.Name.AsString(),
				line.Target.Name.AsString(),
				line.MiddlePoints().ToList(),
				line.Links.Count));


		private static IEnumerable<ModelLink> ToModelLinks(IEnumerable<Link> links) =>
			links.Select(link => new ModelLink(
				link.Source.Name.AsString(),
				link.Target.Name.AsString()));
		

		private static ModelNode ToModelNode(Node node) => 
			new ModelNode(
			node.Name.AsString(),
			node.NodeType.AsString(),
			node.ViewModel?.ItemBounds ?? node.Bounds,
			node.ViewModel?.ItemsViewModel?.ItemsCanvas?.ScaleFactor ?? node.ScaleFactor,
			node.ViewModel?.ItemsViewModel?.ItemsCanvas?.Offset ?? node.Offset,
			node.ViewModel?.Color ?? node.Color,
			node.RootGroup,
			null);
	}
}