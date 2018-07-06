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
			new DataNodeName(node.Name),
			node.Parent != null ? new DataNodeName(node.Parent) : null,
			FromNodeTypeText(node.Type))
		{
			Description = node.Description,
			Bounds = node.Bounds != null ? Rect.Parse(node.Bounds) : RectEx.Zero,
			Scale = node.ItemsScaleFactor,
			Color = node.Color,
			ShowState = node.ShowState
		};


		private static CacheJsonTypes.Node ToCacheJsonNode(DataNode node) => new CacheJsonTypes.Node
		{
			Name = node.Name.FullName,
			Parent = node.Parent.FullName,
			Type = ToNodeTypeText(node.NodeType),
			Description = node.Description,
			Bounds = node.Bounds != RectEx.Zero ? node.Bounds.AsString() : null,
			ItemsScaleFactor = node.Scale,
			Color = node.Color,
			ShowState = node.ShowState,
		};

		private static SaveJsonTypes.Node ToSaveJsonNode(DataNode node) => new SaveJsonTypes.Node
		{
			Bounds = node.Bounds != RectEx.Zero ? node.Bounds.AsString() : null,
			Scale = node.Scale,
			Color = node.Color,
			State = node.ShowState,
		};


		private static NodeType FromNodeTypeText(string nodeType)
		{
			switch (nodeType)
			{
				case CacheJsonTypes.NodeType.Solution:
					return NodeType.Solution;
				case CacheJsonTypes.NodeType.SolutionFolder:
					return NodeType.SolutionFolder;
				case CacheJsonTypes.NodeType.Assembly:
					return NodeType.Assembly;
				case CacheJsonTypes.NodeType.Group:
					return NodeType.Group;
				case CacheJsonTypes.NodeType.Dll:
					return NodeType.Dll;
				case CacheJsonTypes.NodeType.Exe:
					return NodeType.Exe;
				case CacheJsonTypes.NodeType.NameSpace:
					return NodeType.NameSpace;
				case CacheJsonTypes.NodeType.Type:
					return NodeType.Type;
				case CacheJsonTypes.NodeType.Member:
					return NodeType.Member;
				default:
					throw Asserter.FailFast($"Unexpected type {nodeType}");
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
				case NodeType.None:
					return null;
				case NodeType.Solution:
					return CacheJsonTypes.NodeType.Solution;
				case NodeType.Assembly:
					return CacheJsonTypes.NodeType.Assembly;
				case NodeType.Group:
					return CacheJsonTypes.NodeType.Group;
				case NodeType.Dll:
					return CacheJsonTypes.NodeType.Dll;
				case NodeType.Exe:
					return CacheJsonTypes.NodeType.Exe;
				case NodeType.SolutionFolder:
					return CacheJsonTypes.NodeType.SolutionFolder;
				case NodeType.PrivateMember:
					return CacheJsonTypes.NodeType.Member;
				default:
					throw Asserter.FailFast($"Unexpected type {nodeNodeType}");
			}
		}


		private static DataLink ToModelLink(CacheJsonTypes.Link link) => new DataLink(
			new DataNodeName(link.Source),
			new DataNodeName(link.Target),
			true);


		private static CacheJsonTypes.Link ToJsonLink(DataLink link) => new CacheJsonTypes.Link
		{
			Source = link.Source.FullName,
			Target = link.Target.FullName,
		};



		private static IDataItem ToModelLine(CacheJsonTypes.Line line) => new DataLine(
			new DataNodeName(line.Source),
			new DataNodeName(line.Target),
			ToLinePoints(line.Points),
			line.LinkCount);


		private static CacheJsonTypes.Line ToCacheJsonLine(DataLine line) => new CacheJsonTypes.Line
		{
			Source = line.Source.FullName,
			Target = line.Target.FullName,
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