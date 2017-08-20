using System.Windows;
using System.Windows.Media;
using Dependinator.Modeling.Private.Serializing;

namespace Dependinator.Modeling.Private
{
	internal class Convert
	{
	
		public static DataNode ToDataNode(Dtos.Node node) => new DataNode(
			node.Name,
			node.Type,
			node.Bounds != null ? Rect.Parse(node.Bounds) : RectEx.Zero,
			node.ItemsScale,
			node.ItemsOffset != null ? Point.Parse(node.ItemsOffset) : PointEx.Zero,
			node.Color);


		public static Dtos.Node ToDtoNode(DataNode node) => new Dtos.Node
		{
			Name = node.Name,
			Type = node.NodeType,
			Bounds = node.Bounds != RectEx.Zero ? node.Bounds.AsString() : null,
			ItemsScale = node.ItemsScale,
			ItemsOffset = node.ItemsOffset != PointEx.Zero ? node.ItemsOffset.AsString() : null,
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