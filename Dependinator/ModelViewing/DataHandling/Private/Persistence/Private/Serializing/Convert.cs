using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.DataHandling.Dtos;


namespace Dependinator.ModelViewing.DataHandling.Private.Persistence.Private.Serializing
{
	internal class Convert
	{
		public static IDataItem ToModelItem(JsonTypes.Item item)
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


		public static JsonTypes.Item ToJsonItem(IDataItem item)
		{
			switch (item)
			{
				case DataNode modelNode:
					return new JsonTypes.Item { Node = ToJsonNode(modelNode) };
				case DataLink modelLink:
					return new JsonTypes.Item { Link = ToJsonLink(modelLink) };
				case DataLine modelLine:
					return new JsonTypes.Item { Line = ToJsonLine(modelLine) };
			}

			return null;
		}


		private static DataNode ToModelNode(JsonTypes.Node node) => new DataNode(
			NodeId.From(node.Id),
			NodeName.From(node.Name),
			node.Parent,
			FromNodeTypeText(node.Type),
			node.Description,
			node.Bounds != null ? Rect.Parse(node.Bounds) : RectEx.Zero,
			node.ItemsScaleFactor,
			node.Color,
			node.ShowState);


		private static JsonTypes.Node ToJsonNode(DataNode node) => new JsonTypes.Node
		{
			Id = node.Id.AsString(),
			Name = node.Name.FullName,
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
				case NodeType.Type:
					return JsonTypes.NodeType.Type;
				case NodeType.Member:
					return JsonTypes.NodeType.Member;
				default:
					return null;
			}
		}


		private static DataLink ToModelLink(JsonTypes.Link link) => new DataLink(
			NodeId.From(link.SourceId),
			NodeId.From(link.TargetId),
			true);


		private static JsonTypes.Link ToJsonLink(DataLink link) => new JsonTypes.Link
		{
			SourceId = link.SourceId.AsString(),
			TargetId = link.TargetId.AsString(),
		};



		private static IDataItem ToModelLine(JsonTypes.Line line) => new DataLine(
			NodeId.From(line.SourceId),
			NodeId.From(line.TargetId),
			ToLinePoints(line.Points),
			line.LinkCount);


		private static JsonTypes.Line ToJsonLine(DataLine line) => new JsonTypes.Line
		{
			SourceId = line.SourceId.AsString(),
			TargetId = line.TargetId.AsString(),
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