using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;


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
				new DataNodeName(node.Name.FullName),
				new DataNodeName(node.Parent.Name.FullName),
				node.NodeType)
			{
				Description = node.Description,
				Bounds = node.View.ViewModel?.ItemBounds ?? node.View.Bounds,
				Scale = node.View.ViewModel?.ItemsViewModel?.ItemsCanvas?.ScaleFactor ?? node.View.ScaleFactor,
				Color = node.View.ViewModel?.Color ?? node.View.Color,
				ShowState = node.View.IsHidden ? Node.Hidden : null
			};


		private static IEnumerable<DataLine> ToModelLines(IEnumerable<Line> lines) =>
			lines
				.Where(line => !line.IsHidden)
				.Select(line => new DataLine(
					new DataNodeName(line.Source.Name.FullName),
					new DataNodeName(line.Target.Name.FullName),
					line.View.MiddlePoints().ToList(),
					line.LinkCount));


		private static IEnumerable<DataLink> ToModelLinks(IEnumerable<Link> links) =>
			links.Select(link => new DataLink(
				new DataNodeName(link.Source.Name.FullName),
				new DataNodeName(link.Target.Name.FullName)));
	}
}