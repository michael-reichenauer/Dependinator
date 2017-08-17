using System.Windows;
using Dependinator.Modeling.Private.Serializing;

namespace Dependinator.Modeling.Private
{
	internal class Convert
	{
		public static DataNode ToNode(Data.Node node)
		{
			return new DataNode(
				node.Name, 
				node.Type,
				new Rect(node.X, node.Y, node.Width, node.Height),
				node.Scale,
				new Point(node.OffsetX, node.OffsetY),
				node.Color);
		}

		

		public static Data.Node ToDataNode(DataNode node)
		{
			return new Data.Node
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
		}

		public static DataLink ToLink(Data.Link link)
		{
			return new DataLink(link.Source, link.Target);
		}


		public static Data.Link ToDataLink(DataLink link)
		{
			return new Data.Link
			{
				Source = link.Source,
				Target = link.Target,
			};
		}
	}
}