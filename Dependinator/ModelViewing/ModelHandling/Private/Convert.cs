using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.ModelDataHandling;
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
				node.Id,
				node.Name,
				node.Parent.Name.FullName,
				node.NodeType,
				node.Description,
				null,
				node.View.ViewModel?.ItemBounds ?? node.View.Bounds,
				node.View.ViewModel?.ItemsViewModel?.ItemsCanvas?.ScaleFactor ?? node.View.ScaleFactor,
				node.View.ViewModel?.Color ?? node.View.Color,
				node.View.IsHidden ? Node.Hidden : null);


		private static IEnumerable<ModelLine> ToModelLines(IEnumerable<Line> lines) =>
			lines
				.Where(line => !line.IsHidden)
				.Select(line => new ModelLine(
					line.Source.Id,
					line.Target.Id,
					line.View.MiddlePoints().ToList(),
					line.LinkCount));


		private static IEnumerable<ModelLink> ToModelLinks(IEnumerable<Link> links) =>
			links.Select(link => new ModelLink(link.Source.Id, link.Target.Id));
	}
}