using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelHandling.Core;


namespace Dependinator.ModelHandling.ModelPersistence.Private.Serializing
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
			switch (item)
			{
				case ModelNode modelNode:
					return new JsonTypes.Item { Node = ToJsonNode(modelNode) };
				case ModelLink modelLink:
					return new JsonTypes.Item { Link = ToJsonLink(modelLink) };
				case ModelLine modelLine:
					return new JsonTypes.Item { Line = ToJsonLine(modelLine) };
			}
			
			return null;
		}
		

		private static ModelNode ToModelNode(JsonTypes.Node node) => new ModelNode(
			node.Name,
			node.Parent,
			FromNodeTypeText(node.Type),
			node.Description,
			node.Bounds != null ? Rect.Parse(node.Bounds) : RectEx.Zero,
			node.ItemsScaleFactor,
			node.Color,
			node.ShowState);


		private static JsonTypes.Node ToJsonNode(ModelNode node) => new JsonTypes.Node
		{
			Name = node.Name,
			Parent = node.Parent,
			Type = ToNodeTypeText(node.NodeType),
			Description = node.Description,
			Bounds = node.Bounds != RectEx.Zero ? node.Bounds.AsString() : null,
			ItemsScaleFactor = node.ItemsScaleFactor,
			Color = node.Color,
			ShowState = node.ShowState,
		};


		private static NodeType FromNodeTypeText(string nodeType)
		{
			switch (nodeType)
			{
				case JsonTypes.NodeType.NameSpace:
					return NodeType.NameSpace;
				case JsonTypes.NodeType.Type:
					return NodeType.Type;
				case JsonTypes.NodeType.Member:
					return NodeType.Member;
				default:
					return NodeType.None;
			}
		}


		private static string ToNodeTypeText(NodeType nodeNodeType)
		{
			switch (nodeNodeType)
			{
				case NodeType.NameSpace:
					return JsonTypes.NodeType.NameSpace;
				case NodeType.Type :
					return JsonTypes.NodeType.Type;
				case NodeType.Member:
					return JsonTypes.NodeType.Member;
				default:
					return null;
			}
		}


		private static ModelLink ToModelLink(JsonTypes.Link link) => new ModelLink(
			link.Source,
			link.Target,
			NodeType.None);


		private static JsonTypes.Link ToJsonLink(ModelLink link) => new JsonTypes.Link
		{
			Source = link.Source,
			Target = link.Target,
		};



		private static IModelItem ToModelLine(JsonTypes.Line line) => new ModelLine(
			line.Source,
			line.Target,
			ToLinePoints(line.Points),
			line.LinkCount);


		private static JsonTypes.Line ToJsonLine(ModelLine line) => new JsonTypes.Line
		{
			Source = line.Source,
			Target = line.Target,
			Points = ToJsonPoints(line.Points),
			LinkCount = line.LinkCount
		};


		private static IReadOnlyList<Point> ToLinePoints(IEnumerable<string> linePoints)
		{
			if (linePoints == null)
			{
				return new List<Point>();
			}

			return linePoints.Select(Point.Parse).ToList();
		}


		private static List<string> ToJsonPoints(IEnumerable<Point> points)
		{
			if (!points.Any())
			{
				return null;
			}

			return points.Select(point => point.AsString()).ToList();
		}
	}
}