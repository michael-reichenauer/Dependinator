using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal class Convert
	{
		public static List<IDataItem> ToDataItems(IReadOnlyList<Node> nodes)
		{
			List<IDataItem> items = new List<IDataItem>();

			nodes.ForEach(node => items.AddRange(ToModelNodeItems(node)));
			nodes.ForEach(node => items.AddRange(ToModelLinks(node.SourceLinks)));

			return items;
		}


		private static IEnumerable<IDataItem> ToModelNodeItems(Node node)
		{
			yield return ToModelNode(node);

			foreach (DataLine modelLine in ToModelLines(node.SourceLines))
			{
				yield return modelLine;
			}
		}


		private static DataNode ToModelNode(Node node) =>
			new DataNode(
				node.Id,
				node.Name,
				node.Parent.Name.FullName,
				node.NodeType,
				node.Description,
				node.View.ViewModel?.ItemBounds ?? node.View.Bounds,
				node.View.ViewModel?.ItemsViewModel?.ItemsCanvas?.ScaleFactor ?? node.View.ScaleFactor,
				node.View.ViewModel?.Color ?? node.View.Color,
				node.View.IsHidden ? Node.Hidden : null);


		private static IEnumerable<DataLine> ToModelLines(IEnumerable<Line> lines) =>
			lines
				.Where(line => !line.IsHidden)
				.Select(line => new DataLine(
					line.Source.Id,
					line.Target.Id,
					line.View.MiddlePoints().ToList(),
					line.LinkCount));


		private static IEnumerable<DataLink> ToModelLinks(IEnumerable<Link> links) =>
			links.Select(link => new DataLink(link.Source.Id, link.Target.Id));
	}
}