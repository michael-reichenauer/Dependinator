using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.Serializing
{
    internal class Convert
    {
        public static IDataItem ToModelItem(CacheJsonTypes.Item item)
        {
            if (item.Node != null)
            {
                return ToModelNode(item.Node);
            }

            if (item.Link != null)
            {
                return ToModelLink(item.Link);
            }

            if (item.Line != null)
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
                    return new CacheJsonTypes.Item {Node = ToCacheJsonNode(modelNode)};
                case DataLink modelLink:
                    return new CacheJsonTypes.Item {Link = ToJsonLink(modelLink)};
                case DataLine modelLine:
                    return new CacheJsonTypes.Item {Line = ToCacheJsonLine(modelLine)};
                default:
                    throw Asserter.FailFast($"Unsupported item {item?.GetType()}");
            }
        }


        private static DataNode ToModelNode(CacheJsonTypes.Node node) => new DataNode(
            (DataNodeName)node.Name,
            node.Parent != null ? (DataNodeName)node.Parent : null,
            FromNodeTypeText(node.Type))
        {
            Description = node.Description,
            Bounds = node.Bounds != null ? Rect.Parse(node.Bounds) : RectEx.Zero,
            Scale = node.Scale,
            Color = node.Color,
            ShowState = node.State
        };


        private static CacheJsonTypes.Node ToCacheJsonNode(DataNode node) => new CacheJsonTypes.Node
        {
            Name = (string)node.Name,
            Parent = (string)node.Parent,
            Type = ToNodeTypeText(node.NodeType),
            Description = node.Description,
            Bounds = node.Bounds != RectEx.Zero ? node.Bounds.AsIntString() : null,
            Scale = node.Scale,
            Color = node.Color,
            State = node.ShowState
        };


        public static SaveJsonTypes.Node ToSaveJsonNode(DataNode node) => new SaveJsonTypes.Node
        {
            Name = (string)node.Name,
            Bounds = node.Bounds != RectEx.Zero ? node.Bounds.AsIntString() : null,
            Scale = node.Scale.Round(3)
        };


        public static SaveJsonTypes.Line ToSaveJsonLine(DataLine line) => new SaveJsonTypes.Line
        {
            Target = (string)line.Target,
            Points = ToJsonPoints(line.Points)
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
            (DataNodeName)link.Source,
            (DataNodeName)link.Target,
            true);


        private static CacheJsonTypes.Link ToJsonLink(DataLink link) => new CacheJsonTypes.Link
        {
            Source = (string)link.Source,
            Target = (string)link.Target
        };


        private static IDataItem ToModelLine(CacheJsonTypes.Line line) => new DataLine(
            (DataNodeName)line.Source,
            (DataNodeName)line.Target,
            ToLinePoints(line.Points),
            line.LinkCount);


        private static CacheJsonTypes.Line ToCacheJsonLine(DataLine line) => new CacheJsonTypes.Line
        {
            Source = (string)line.Source,
            Target = (string)line.Target,
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
