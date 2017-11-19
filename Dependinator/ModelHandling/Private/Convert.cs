using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelHandling.Private
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
				line.Source.Name.FullName,
				line.Target.Name.FullName,
				line.MiddlePoints().ToList(),
				line.Links.Count));


		private static IEnumerable<ModelLink> ToModelLinks(IEnumerable<Link> links) =>
			links.Select(link => new ModelLink(
				link.Source.Name.FullName,
				link.Target.Name.FullName,
				link.Target.NodeType));


		private static ModelNode ToModelNode(Node node) =>
			new ModelNode(
				node.Name.FullName,
				node.Parent.Name.FullName,
				node.NodeType,
				node.ViewModel?.ItemBounds ?? node.Bounds,
				node.ViewModel?.ItemsViewModel?.ItemsCanvas?.ScaleFactor ?? node.ScaleFactor,
				node.ViewModel?.ItemsViewModel?.ItemsCanvas?.Offset ?? node.Offset,
				node.ViewModel?.Color ?? node.Color,
				node.IsHidden ? Node.Hidden : null);
	}
}