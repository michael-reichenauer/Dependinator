using System.Windows;
using System.Windows.Media;

namespace Dependinator.ModelParsing.Private
{
	internal class Convert
	{
		public static ModelItem ToModelItem(JsonTypes.Item item) => new ModelItem(
			item.Node != null ? ToModelNode(item.Node) : null,
			item.Link != null ? ToModelLink(item.Link) : null);


		public static JsonTypes.Item ToJsonItem(ModelItem item) => new JsonTypes.Item
		{
			Node = item.Node != null ? ToJsonNode(item.Node) : null,
			Link = item.Link != null ? ToJsonLink(item.Link) : null
		};


		private static ModelNode ToModelNode(JsonTypes.Node node) => new ModelNode(
			node.Name,
			node.Type,
			node.Bounds != null ? Rect.Parse(node.Bounds) : RectEx.Zero,
			node.ItemsScale,
			node.ItemsOffset != null ? Point.Parse(node.ItemsOffset) : PointEx.Zero,
			node.Color);


		private static JsonTypes.Node ToJsonNode(ModelNode node) => new JsonTypes.Node
		{
			Name = node.Name,
			Type = node.NodeType,
			Bounds = node.Bounds != RectEx.Zero ? node.Bounds.AsString() : null,
			ItemsScale = node.ItemsScale,
			ItemsOffset = node.ItemsOffset != PointEx.Zero ? node.ItemsOffset.AsString() : null,
			Color = node.Color
		};


		private static ModelLink ToModelLink(JsonTypes.Link link) => new ModelLink(
			link.Source,
			link.Target);


		private static JsonTypes.Link ToJsonLink(ModelLink link) => new JsonTypes.Link
		{
			Source = link.Source,
			Target = link.Target,
		};
	}
}