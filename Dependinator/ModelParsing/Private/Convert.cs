using System.Linq;
using System.Windows;
using System.Windows.Media;


namespace Dependinator.ModelParsing.Private
{
	internal class Convert
	{
		public static IModelItem ToModelItem(JsonTypes.Item item)
		{
			if (item.Node != null)
			{
				return ToModelNode(item.Node);
			}
			else if (item.Link != null)
			{
				return ToModelLink(item.Link);
			}
			else if (item.Line != null)
			{
				return ToModelLine(item.Line);
			}

			return null;
		}



		public static JsonTypes.Item ToJsonItem(IModelItem item)
		{
			if (item is ModelNode modelNode)
			{
				return new JsonTypes.Item { Node = ToJsonNode(modelNode) };
			}
			else if (item is ModelLink modelLink)
			{
				return new JsonTypes.Item { Link = ToJsonLink(modelLink) };
			}
			else if (item is ModelLine modelLine)
			{
				return new JsonTypes.Item { Line = ToJsonLine(modelLine) };
			}


			return null;
		}



		private static ModelNode ToModelNode(JsonTypes.Node node) => new ModelNode(
			node.Name,
			node.Type,
			node.Bounds != null ? Rect.Parse(node.Bounds) : RectEx.Zero,
			node.ItemsScaleFactor,
			node.ItemsOffset != null ? Point.Parse(node.ItemsOffset) : PointEx.Zero,
			node.Color,
			node.RootGroup);


		private static JsonTypes.Node ToJsonNode(ModelNode node) => new JsonTypes.Node
		{
			Name = node.Name,
			Type = node.NodeType,
			Bounds = node.Bounds != RectEx.Zero ? node.Bounds.AsString() : null,
			ItemsScaleFactor = node.ItemsScaleFactor,
			ItemsOffset = node.ItemsOffset != PointEx.Zero ? node.ItemsOffset.AsString() : null,
			Color = node.Color,
			RootGroup = node.RootGroup
		};


		private static ModelLink ToModelLink(JsonTypes.Link link) => new ModelLink(
			link.Source,
			link.Target);


		private static JsonTypes.Link ToJsonLink(ModelLink link) => new JsonTypes.Link
		{
			Source = link.Source,
			Target = link.Target,
		};



		private static IModelItem ToModelLine(JsonTypes.Line line) => new ModelLine(
			line.Source,
			line.Target,
			line.LinkCount);


		private static JsonTypes.Line ToJsonLine(ModelLine line) => new JsonTypes.Line
		{
			Source = line.Source,
			Target = line.Target,
			LinkCount = line.LinkCount
		};
	}
}