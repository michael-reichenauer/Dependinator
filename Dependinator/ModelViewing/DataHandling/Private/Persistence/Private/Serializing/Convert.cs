using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.DataHandling.Private.Persistence.Private.Serializing
{
	internal class Convert
	{
		public static IDataItem ToModelItem(CacheJsonTypes.Item item)
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


		public static CacheJsonTypes.Item ToCacheJsonItem(IDataItem item)
		{
			switch (item)
			{
				case DataNode modelNode:
					return new CacheJsonTypes.Item { Node = ToCacheJsonNode(modelNode) };
				case DataLink modelLink:
					return new CacheJsonTypes.Item { Link = ToJsonLink(modelLink) };
				case DataLine modelLine:
					return new CacheJsonTypes.Item { Line = ToCacheJsonLine(modelLine) };
				default:
					throw Asserter.FailFast($"Unsupported item {item?.GetType()}");
			}
		}

		public static SaveJsonTypes.Item ToSaveJsonItem(IDataItem item)
		{
			switch (item)
			{
				case DataNode modelNode:
					return new SaveJsonTypes.Item { Node = ToSaveJsonNode(modelNode) };
			}

			return null;
		}



		private static DataNode ToModelNode(CacheJsonTypes.Node node) => new DataNode(
			NodeId.From(node.Id),
			NodeName.From(node.Name),
			node.Parent,
			FromNodeTypeText(node.Type),
			node.Description,
			node.Bounds != null ? Rect.Parse(node.Bounds) : RectEx.Zero,
			node.ItemsScaleFactor,
			node.Color,
			node.ShowState);


		private static CacheJsonTypes.Node ToCacheJsonNode(DataNode node) => new CacheJsonTypes.Node
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

		private static SaveJsonTypes.Node ToSaveJsonNode(DataNode node) => new SaveJsonTypes.Node
		{
			Id = node.Id.AsString(),
			Bounds= node.Bounds != RectEx.Zero ? node.Bounds.AsString() : null,
			Scale = node.ItemsScaleFactor,
			Color = node.Color,
			State = node.ShowState,
		};


		private static NodeType FromNodeTypeText(string nodeType)
		{
			switch (nodeType)
			{
				case CacheJsonTypes.NodeType.NameSpace:
					return NodeType.NameSpace;
				case CacheJsonTypes.NodeType.Type:
					return NodeType.Type;
				case CacheJsonTypes.NodeType.Member:
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
					return CacheJsonTypes.NodeType.NameSpace;
				case NodeType.Type:
					return CacheJsonTypes.NodeType.Type;
				case NodeType.Member:
					return CacheJsonTypes.NodeType.Member;
				default:
					return null;
			}
		}


		private static DataLink ToModelLink(CacheJsonTypes.Link link) => new DataLink(
			NodeId.From(link.SourceId),
			NodeId.From(link.TargetId),
			true);


		private static CacheJsonTypes.Link ToJsonLink(DataLink link) => new CacheJsonTypes.Link
		{
			SourceId = link.SourceId.AsString(),
			TargetId = link.TargetId.AsString(),
		};



		private static IDataItem ToModelLine(CacheJsonTypes.Line line) => new DataLine(
			NodeId.From(line.SourceId),
			NodeId.From(line.TargetId),
			ToLinePoints(line.Points),
			line.LinkCount);


		private static CacheJsonTypes.Line ToCacheJsonLine(DataLine line) => new CacheJsonTypes.Line
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