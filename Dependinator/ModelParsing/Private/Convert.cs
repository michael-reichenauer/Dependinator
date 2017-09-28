using System.Windows;
using System.Windows.Media;
using Dependinator.Utils;


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

			return null;
		}




		public static JsonTypes.Item ToJsonItem(IModelItem item)
		{
			if (item is ModelNode modelNode)
			{
				return new JsonTypes.Item {Node = ToJsonNode(modelNode)};
			}
			else if (item is ModelLink modelLink)
			{
				return new JsonTypes.Item { Link = ToJsonLink(modelLink) };
			}


			return null;

			//Node = item.Node != null ? ToJsonNode(item.Node) : null,
			//Link = item.Link != null ? ToJsonLink(item.Link) : null
		}


		private static ModelNode ToModelNode(JsonTypes.Node node) => new ModelNode(
			new NodeName(node.Name),
			node.Type,
			node.Bounds != null ? Rect.Parse(node.Bounds) : RectEx.Zero,
			node.ItemsScaleFactor,
			node.ItemsOffset != null ? Point.Parse(node.ItemsOffset) : PointEx.Zero,
			node.Color,
			node.RootGroup);


		private static JsonTypes.Node ToJsonNode(ModelNode node) => new JsonTypes.Node
		{
			Name = node.Name.FullName,
			Type = node.NodeType,
			Bounds = node.Bounds != RectEx.Zero ? node.Bounds.AsString() : null,
			ItemsScaleFactor = node.ItemsScaleFactor,
			ItemsOffset = node.ItemsOffset != PointEx.Zero ? node.ItemsOffset.AsString() : null,
			Color = node.Color,
			RootGroup = node.RootGroup
		};


		private static ModelLink ToModelLink(JsonTypes.Link link) => new ModelLink(
			new NodeName(link.Source),
			new NodeName(link.Target));


		private static JsonTypes.Link ToJsonLink(ModelLink link) => new JsonTypes.Link
		{
			Source = link.Source.FullName,
			Target = link.Target.FullName,
		};
	}
}