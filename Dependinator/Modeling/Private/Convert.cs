using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Dependinator.Modeling.Private.Serializing;

namespace Dependinator.Modeling.Private
{
	internal class Convert
	{
		private static readonly Rect RectNone = new Rect(0, 0, 0, 0);
		private static readonly Point PointNone = new Point(0, 0);

		public static DataNode ToDataNode(Dtos.Node node) => new DataNode(
			node.Name,
			node.Type,
			node.Bounds != null ? Rect.Parse(node.Bounds) : RectNone,
			node.Scale,
			node.Offset != null ? Point.Parse(node.Offset) : PointNone,
			node.Color);


		public static Dtos.Node ToDtoNode(DataNode node) => new Dtos.Node
		{
			Name = node.Name,
			Type = node.NodeType,
			Bounds = node.Bounds != RectNone ? node.Bounds.AsString() : null,
			Scale = node.Scale,
			Offset = node.Offset != PointNone ? node.Offset.AsString() : null,
			Color = node.Color
		};


		public static DataLink ToDataLink(Dtos.Link link) => new DataLink(link.Source, link.Target);


		public static Dtos.Link ToDtoLink(DataLink link) => new Dtos.Link
		{
			Source = link.Source,
			Target = link.Target,
		};


		public static Dtos.Item ToDtoItem(DataItem item) => new Dtos.Item
		{
			Node = item.Node != null ? ToDtoNode(item.Node) : null,
			Link = item.Link != null ? ToDtoLink(item.Link) : null
		};


		public static DataItem ToDataItem(Dtos.Item item) => new DataItem(
			item.Node != null ? ToDataNode(item.Node) : null,
			item.Link != null ? ToDataLink(item.Link) : null);

	}
}