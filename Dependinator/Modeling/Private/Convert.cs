using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.Modeling.Private.Serializing;

namespace Dependinator.Modeling.Private
{
	internal class Convert
	{
		public static DataNode ToDataNode(Dtos.Node node) => new DataNode(
			node.Name, 
			node.Type,
			new Rect(node.X, node.Y, node.Width, node.Height),
			node.Scale,
			new Point(node.OffsetX, node.OffsetY),
			node.Color);


		public static Dtos.Node ToDtoNode(DataNode node) => new Dtos.Node
		{
			Name = node.Name,
			Type = node.NodeType,
			X = node.Bounds.X,
			Y = node.Bounds.Y,
			Width = node.Bounds.Width,
			Height = node.Bounds.Height,
			Scale = node.Scale,
			OffsetX = node.Offset.X,
			OffsetY = node.Offset.Y,
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

	}
}