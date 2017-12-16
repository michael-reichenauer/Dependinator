using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal class Convert
	{
		public static List<IModelItem> ToDataItems(IReadOnlyList<Node> nodes)
		{
			List<IModelItem> items = new List<IModelItem>();

			nodes.ForEach(node => items.AddRange(ToModelNodeItems(node)));
			nodes.ForEach(node => items.AddRange(ToModelLinks(node.SourceLinks)));

			return items;
		}


		private static IEnumerable<IModelItem> ToModelNodeItems(Node node)
		{
			yield return ToModelNode(node);

			foreach (ModelLine modelLine in ToModelLines(node.SourceLines))
			{
				yield return modelLine;
			}
		}


		private static ModelNode ToModelNode(Node node) =>
			new ModelNode(
				node.Name.FullName,
				node.Parent.Name.FullName,
				node.NodeType,
				node.Description,
				node.ViewModel?.ItemBounds ?? node.Bounds,
				node.ViewModel?.ItemsViewModel?.ItemsCanvas?.ScaleFactor ?? node.ScaleFactor,
				node.ViewModel?.Color ?? node.Color,
				node.IsHidden ? Node.Hidden : null);


		private static IEnumerable<ModelLine> ToModelLines(IEnumerable<Line> lines) =>
			lines.Select(line => new ModelLine(
				line.Source.Name.FullName,
				line.Target.Name.FullName,
				line.Target.NodeType,
				line.MiddlePoints().ToList(),
				line.Links.Count));


		private static IEnumerable<ModelLink> ToModelLinks(IEnumerable<Link> links) =>
			links.Select(link => new ModelLink(
				link.Source.Name.FullName,
				link.Target.Name.FullName,
				link.Target.NodeType));
	}
}